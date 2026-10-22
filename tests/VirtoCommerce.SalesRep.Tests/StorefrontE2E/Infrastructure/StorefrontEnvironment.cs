using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
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
/// (a clone's Vite dev server proxying to the backend, or a build the backend serves itself), and one browser. Tests
/// in the group share all of it; each test gets its own signed-in browser context. Groups run one after another
/// (their collections disable parallelization), so only one environment is alive at a time.
/// </summary>
public abstract class StorefrontEnvironment : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private ViteDevServer _vite;

    internal InProcessBackend Backend { get; private set; }

    /// <summary>Where the browser opens the storefront: the Vite dev server, or the backend's own origin for a build.</summary>
    internal string StorefrontUrl { get; private set; }

    internal SalesRepTestContext Context => Backend.Context;

    /// <summary>Timestamped phases of this environment's life, so a hang or a slow step is attributable.</summary>
    public string PhaseLog { get; } = Path.Combine(DiagnosticsDirectory, $"environment-{DateTime.Now:HHmmss}.log");

    public static string DiagnosticsDirectory => Path.Combine(AppContext.BaseDirectory, "StorefrontE2E-diagnostics");

    public async ValueTask InitializeAsync()
    {
        // Before anything starts: the storefront, and the plugin's build that every hub page comes from. Either one
        // missing fails the group with what to do.
        var storefront = StorefrontLocation.Current;
        StorefrontPlugin.EnsureBuilt();

        Log("backend starting");
        Backend = await InProcessBackend.StartAsync(storefront.BuildDirectory, ConfigureServices);
        Log($"backend ready at {Backend.Url}");

        await SeedAsync(Backend.Context);
        Log("seeded");

        if (storefront.BuildDirectory != null)
        {
            StorefrontUrl = Backend.Url;
        }
        else
        {
            _vite = await ViteDevServer.StartAsync(storefront.SourceDirectory, Backend.Url, DiagnosticsDirectory);
            StorefrontUrl = _vite.Url;
        }

        Log($"storefront ready at {StorefrontUrl}");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1",
        });
        Log("browser launched");
    }

    private void Log(string phase)
    {
        Directory.CreateDirectory(DiagnosticsDirectory);
        File.AppendAllText(PhaseLog, $"{DateTime.Now:HH:mm:ss.fff} [{GetType().Name}] {phase}{Environment.NewLine}");
    }

    /// <summary>The group's shared data, seeded through the harness before the storefront starts.</summary>
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

    /// <summary>The first matching task row, or null after ten seconds, read between requests.</summary>
    internal async Task<WorkTaskEntity> WaitForTaskAsync(Expression<Func<WorkTaskEntity, bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (true)
        {
            var task = await Backend.RunExclusiveAsync(async context =>
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
        => Backend.RunExclusiveAsync(async context =>
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
        var token = await Backend.Tokens.IssueAsync(userId, organizationId);

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
        var session = new StorefrontSession(context, page);

        // Vite compiles on first request, so with a clone the first navigation of a group is the slow one.
        await page.GotoAsync(path, new PageGotoOptions { Timeout = 120_000, WaitUntil = WaitUntilState.Load });
        Log($"loaded {page.Url}");

        return session;
    }

    public async ValueTask DisposeAsync()
    {
        if (Backend == null)
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

        if (_vite != null)
        {
            await _vite.DisposeAsync();
        }

        Log("storefront stopped");

        if (Backend != null)
        {
            await Backend.DisposeAsync();
        }

        Log("backend stopped");
    }
}
