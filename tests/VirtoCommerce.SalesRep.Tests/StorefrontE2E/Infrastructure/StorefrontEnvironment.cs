using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.TaskManagement.Data.Models;
using Xunit;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// One virtual environment per test group (xunit collection): the in-process backend, its seeded data, the storefront
/// (the run's Vite dev server for a clone, proxying to the backend, or a build the backend serves itself), and one
/// browser. Tests in the group share all of it; each test gets its own signed-in browser context. Groups run one after
/// another (their collections disable parallelization), so only one environment is alive at a time.
/// </summary>
public abstract class StorefrontEnvironment : IAsyncLifetime
{
    private readonly Lock _logLock = new();
    private IPlaywright _playwright;
    private IBrowser _browser;
    private InProcessBackend _backend;
    private int _reps;

    /// <summary>Where the browser opens the storefront: the run's Vite dev server, or the backend's own origin for a build.</summary>
    internal string StorefrontUrl { get; private set; }

    /// <summary>
    /// Timestamped phases of this environment's life, so a hang or a slow step is attributable, with its pages' console
    /// errors and warnings among them.
    /// </summary>
    public string PhaseLog { get; } = Path.Combine(DiagnosticsDirectory, $"environment-{DateTime.Now:HHmmss}.log");

    public static string DiagnosticsDirectory => Path.Combine(AppContext.BaseDirectory, "StorefrontE2E-diagnostics");

    public async ValueTask InitializeAsync()
    {
        // Before anything starts: the storefront, and the plugin's build that every hub page comes from. Either one
        // missing fails the group with what to do.
        var storefront = StorefrontLocation.Current;
        StorefrontPlugin.EnsureBuilt();

        // A build is served by the backend itself, from any free port. A clone runs on the run's Vite dev server, whose
        // proxy target was fixed when it started: the group's backend takes the port it points at.
        var port = storefront.BuildDirectory == null ? SharedViteDevServer.BackendPort : 0;

        Log("backend starting");
        _backend = await InProcessBackend.StartAsync(storefront.BuildDirectory, ConfigureServices, port);
        Log($"backend ready at {_backend.Url}");

        await SeedAsync(_backend.Context);
        Log("seeded");

        if (storefront.BuildDirectory != null)
        {
            StorefrontUrl = _backend.Url;
        }
        else
        {
            var vite = await SharedViteDevServer.GetAsync(storefront.SourceDirectory, DiagnosticsDirectory);
            StorefrontUrl = vite.Url;
        }

        Log($"storefront ready at {StorefrontUrl}");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1",
        });
        Log("browser launched");

        await EnsureHubLoadsAsync();
        Log("hub loaded");
    }

    /// <summary>
    /// The hub's menu, which the storefront draws only once it has loaded the plugin, checked before the group's tests.
    /// A storefront that refuses the plugin (one older than its <c>requiredHostVersion</c>, say) would fail every test in
    /// turn, each after its own minute; this fails the group at once. A development build (a clone) logs why it refused,
    /// so the check stops at that line. A production build logs nothing, so its failure names both versions instead.
    /// </summary>
    private async Task EnsureHubLoadsAsync()
    {
        var rep = await NewRepAsync();
        await using var session = await OpenAsync(rep.UserId, "/company/my-customers");

        await session.RunAsync($"{GetType().Name}_HubLoads", async page =>
        {
            var menuLink = page.GetByRole(AriaRole.Link, new() { Name = "My activity", Exact = true });
            var deadline = DateTime.UtcNow.AddSeconds(60);

            while (!await menuLink.IsVisibleAsync())
            {
                var refusal = session.ConsoleMessages.FirstOrDefault(x => x.Contains("[MF] Skipping", StringComparison.Ordinal));
                if (refusal != null)
                {
                    throw new InvalidOperationException($"The storefront refused the hub plugin: {refusal}");
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new InvalidOperationException(
                        "The hub did not load: no \"My activity\" link in its menu after 60 seconds. The plugin requires " +
                        $"@vc-frontend/core {StorefrontPlugin.ReadRequiredHostVersion() ?? "(none declared)"}, the storefront provides " +
                        $"{StorefrontLocation.Current.ReadCoreVersion() ?? "(unknown for a build alone)"}.");
                }

                await Task.Delay(250);
            }
        });
    }

    private void Log(string line)
    {
        // Console lines come in on Playwright's thread, phases on the test's.
        lock (_logLock)
        {
            Directory.CreateDirectory(DiagnosticsDirectory);
            File.AppendAllText(PhaseLog, $"{DateTime.Now:HH:mm:ss.fff} [{GetType().Name}] {line}{Environment.NewLine}");
        }
    }

    /// <summary>The group's shared data, seeded through the harness before the first page opens.</summary>
    private protected abstract Task SeedAsync(SalesRepTestContext context);

    /// <summary>The browser's IANA time zone; null keeps the machine's. A group that checks times of day pins it.</summary>
    private protected virtual string BrowserTimeZone => null;

    /// <summary>A group's own doubles, registered after the storefront shell's, so they win: another module list, say.</summary>
    private protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>A task the rep creates through the real mutation the storefront uses; the due date goes in to the second.</summary>
    private protected static async Task<string> CreateTaskAsync(SalesRepTestContext context, SalesRepDetails rep, string name, DateTime dueDate,
        string type = null, string customerId = null, string priority = null, string description = null)
    {
        var due = dueDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var fields = new (string Name, string Value)[] { ("type", type), ("customerId", customerId), ("priority", priority), ("description", description) }
            .Where(x => x.Value != null)
            .Select(x => $", {x.Name}: \"{x.Value}\"");
        var json = await ExecuteAsRepAsync(context, rep, $"mutation {{ createSalesRepTask(command: {{ name: \"{name}\", dueDate: \"{due}\"{string.Concat(fields)} }}) {{ id }} }}");

        return SalesRepTestContext.Node(json, "createSalesRepTask").GetProperty("id").GetString();
    }

    /// <summary>A GraphQL operation as the rep, who holds the permission every hub operation requires.</summary>
    private protected static Task<string> ExecuteAsRepAsync(SalesRepTestContext context, SalesRepDetails rep, string operation)
        => context.ExecuteGraphQlAsync(operation, userId: rep.UserId, memberId: rep.Id, permissions: [SalesRepModuleConstants.Security.Permissions.Access]);

    /// <summary>
    /// Test-side work against the harness, between requests: the page may still be fetching, and each in-memory
    /// database takes one caller at a time.
    /// </summary>
    internal Task<T> RunExclusiveAsync<T>(Func<SalesRepTestContext, Task<T>> work) => _backend.RunExclusiveAsync(work);

    /// <summary>
    /// A rep of the test's own, serving the given customers. What the test adds or takes away is nobody else's: its
    /// tasks never push another test's off the task list's first page, and nothing it changes needs undoing.
    /// </summary>
    internal Task<SalesRepDetails> NewRepAsync(params string[] customerIds)
    {
        var n = (++_reps).ToString(CultureInfo.InvariantCulture);
        return RunExclusiveAsync(context => context.CreateRepInStoreAsync("Rep", n, $"rep{n}@e2e.local", InProcessBackend.StoreId, customerIds));
    }

    /// <summary>A task the rep creates through the real mutation, between requests.</summary>
    internal Task<string> AddTaskAsync(SalesRepDetails rep, string name, DateTime dueDate)
        => RunExclusiveAsync(context => CreateTaskAsync(context, rep, name, dueDate));

    /// <summary>The first matching task row, or null after ten seconds, read between requests.</summary>
    internal async Task<WorkTaskEntity> WaitForTaskAsync(Expression<Func<WorkTaskEntity, bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (true)
        {
            var task = await RunExclusiveAsync(async context =>
            {
                using var db = context.NewTaskManagementDbContext();
                return await db.Set<WorkTaskEntity>().AsNoTracking().FirstOrDefaultAsync(predicate);
            });

            if (task != null || DateTime.UtcNow > deadline)
            {
                return task;
            }

            await Task.Delay(250);
        }
    }

    /// <summary>How many task rows match, read between requests.</summary>
    internal Task<int> CountTasksAsync(Expression<Func<WorkTaskEntity, bool>> predicate)
        => RunExclusiveAsync(async context =>
        {
            using var db = context.NewTaskManagementDbContext();
            return await db.Set<WorkTaskEntity>().CountAsync(predicate);
        });

    /// <summary>
    /// A fresh browser context signed in as the account (a token minted by the backend from the real claim
    /// producers, stored the way the storefront's useAuth.ts stores it), landed on the storefront path. The browser
    /// runs in <paramref name="timeZoneId"/> when given, else in the group's zone. <paramref name="beforeLoad"/>
    /// prepares the context before the first navigation, routes included.
    /// </summary>
    internal async Task<StorefrontSession> OpenAsync(string userId, string path, string organizationId = null, string timeZoneId = null, Func<IBrowserContext, Task> beforeLoad = null)
    {
        Log($"opening {path} as {userId}");
        var token = await _backend.Tokens.IssueAsync(userId, organizationId);

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = StorefrontUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            TimezoneId = timeZoneId ?? BrowserTimeZone,
        });

        await context.AddInitScriptAsync(
            $"localStorage.setItem('auth', {JsonSerializer.Serialize(token.ToLocalStorageJson())});" +
            $"localStorage.setItem('user-id', {JsonSerializer.Serialize(userId)});");

        if (beforeLoad != null)
        {
            await beforeLoad(context);
        }

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);

        // Into the phase log as well, which a failed CI run uploads. Subscribed before the first navigation, so the
        // boot's are in.
        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                Log($"console [{message.Type}] {message.Text}");
            }
        };

        var session = new StorefrontSession(context, page);

        // Vite compiles on first request, so with a clone the run's first navigation is the slow one.
        await page.GotoAsync(path, new PageGotoOptions { Timeout = 120_000, WaitUntil = WaitUntilState.Load });
        Log($"loaded {page.Url}");

        return session;
    }

    public async ValueTask DisposeAsync()
    {
        if (_backend == null)
        {
            return;
        }

        Log("disposing");

        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        Log("browser closed");

        // A clone's dev server stays up for the next group: SharedViteDevServer stops it when the run ends.
        await _backend.DisposeAsync();
        Log("backend stopped");
    }
}
