using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// One virtual environment per test group (xunit collection): the in-process backend serving the built VC-Shell app,
/// its seeded data, and one browser. Tests in the group share all of it; each test gets its own signed-in browser
/// context. Groups run one after another (their collections disable parallelization), so only one environment is
/// alive at a time. No node process is involved: the app is static files on the backend's own origin.
/// </summary>
public abstract class VCShellAppEnvironment : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;

    internal InProcessVCShellBackend Backend { get; private set; }

    internal SalesRepTestContext Context => Backend.Context;

    /// <summary>Timestamped phases of this environment's life, so a hang or a slow step is attributable.</summary>
    public string PhaseLog { get; } = Path.Combine(DiagnosticsDirectory, $"environment-{DateTime.Now:HHmmss}.log");

    public static string DiagnosticsDirectory => Path.Combine(AppContext.BaseDirectory, "VCShellAppE2E-diagnostics");

    public async ValueTask InitializeAsync()
    {
        // Every test in the group skips itself without a built app; the environment must not fail first.
        if (!VCShellAppAvailability.IsAvailable)
        {
            return;
        }

        Log("backend starting");
        Backend = await InProcessVCShellBackend.StartAsync(VCShellAppAvailability.AppDirectory);
        Log($"backend ready at {Backend.Url}, app at {Backend.AppUrl}");

        await SeedAsync(Backend.Context);
        Log("seeded");

        _playwright = await Playwright.CreateAsync();
        // The framework marks menu items, toolbar buttons, selects and options with data-test-id.
        _playwright.Selectors.SetTestIdAttribute("data-test-id");
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

    /// <summary>The group's shared data, seeded through the harness before the browser starts.</summary>
    private protected abstract Task SeedAsync(SalesRepTestContext context);

    /// <summary>
    /// A fresh browser context signed in as the account, landed on the app route (<c>sales-reps</c> is the list
    /// blade). The framework authenticates with the platform's Identity cookie and nothing else, so the context's
    /// request client (it shares the cookie jar) obtains that cookie from the backend before the first navigation;
    /// the principal behind it comes from the real claim producers, as a sign-in through the page would.
    /// </summary>
    internal async Task<VCShellSession> OpenAsync(string userId, string route = "")
    {
        Log($"opening {route} as {userId}");

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Backend.Url,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        });

        var signIn = await context.APIRequest.PostAsync($"/e2e/sign-in?userId={Uri.EscapeDataString(userId)}");
        if (!signIn.Ok)
        {
            throw new InvalidOperationException($"Signing in as '{userId}' answered {signIn.Status}: {await signIn.TextAsync()}");
        }

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        var session = new VCShellSession(context, page);

        // The app's router uses hash history under the base path: /apps/vc-sales-rep/#/sales-reps.
        await page.GotoAsync($"{VCShellServices.AppPath}/#/{route.TrimStart('/')}", new PageGotoOptions { Timeout = 60_000, WaitUntil = WaitUntilState.Load });
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

        await Backend.DisposeAsync();
        Log("backend stopped");
    }
}
