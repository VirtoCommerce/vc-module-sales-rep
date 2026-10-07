using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// One virtual environment per test group (xunit collection): the in-process backend, its seeded data, a storefront
/// dev server proxying to it, and one browser. Tests in the group share all of it; each test gets its own signed-in
/// browser context. Groups run one after another (their collections disable parallelization), so only one
/// environment is alive at a time.
/// </summary>
public abstract class StorefrontEnvironment : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;

    internal InProcessBackend Backend { get; private set; }

    internal ViteDevServer Storefront { get; private set; }

    internal SalesRepTestContext Context => Backend.Context;

    /// <summary>Timestamped phases of this environment's life, so a hang or a slow step is attributable.</summary>
    public string PhaseLog { get; } = Path.Combine(DiagnosticsDirectory, $"environment-{DateTime.Now:HHmmss}.log");

    public static string DiagnosticsDirectory => Path.Combine(AppContext.BaseDirectory, "StorefrontE2E-diagnostics");

    public async ValueTask InitializeAsync()
    {
        // Every test in the group skips itself without a storefront checkout; the environment must not fail first.
        if (!StorefrontAvailability.IsAvailable)
        {
            return;
        }

        Log("backend starting");
        Backend = await InProcessBackend.StartAsync();
        Log($"backend ready at {Backend.Url}");

        await SeedAsync(Backend.Context);
        Log("seeded");

        Storefront = await ViteDevServer.StartAsync(ViteDevServer.ResolveFrontendDirectory(), Backend.Url, DiagnosticsDirectory);
        Log($"storefront ready at {Storefront.Url}");

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

    /// <summary>
    /// A fresh browser context signed in as the account (a token minted by the backend from the real claim
    /// producers, stored the way the storefront's useAuth.ts stores it), landed on the storefront path.
    /// </summary>
    internal async Task<StorefrontSession> OpenAsync(string userId, string path, string organizationId = null)
    {
        Log($"opening {path} as {userId}");
        var token = await Backend.Tokens.IssueAsync(userId, organizationId);

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Storefront.Url,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        });

        await context.AddInitScriptAsync(
            $"localStorage.setItem('auth', {JsonSerializer.Serialize(token.ToLocalStorageJson())});" +
            $"localStorage.setItem('user-id', {JsonSerializer.Serialize(userId)});");

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        var session = new StorefrontSession(context, page);

        // The dev server compiles on first request, so the first navigation of a group is the slow one.
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

        if (Storefront != null)
        {
            await Storefront.DisposeAsync();
        }

        Log("storefront stopped");

        if (Backend != null)
        {
            await Backend.DisposeAsync();
        }

        Log("backend stopped");
    }
}
