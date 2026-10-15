using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// One signed-in browser context on the VC-Shell app. Records what the app would otherwise only show on screen
/// (console errors, dialogs, API responses of 400 and above, failed requests), so a failing test reports them with
/// a full-page screenshot instead of a bare locator timeout.
/// </summary>
internal sealed class VCShellSession : IAsyncDisposable
{
    private readonly IBrowserContext _context;
    private readonly List<string> _console = [];
    private readonly List<string> _dialogs = [];
    private readonly List<string> _apiErrors = [];
    private readonly List<string> _failedRequests = [];

    public VCShellSession(IBrowserContext context, IPage page)
    {
        _context = context;
        Page = page;

        page.Console += (_, message) =>
        {
            // The host has no SignalR hub; the framework retries the negotiation forever and logs each attempt.
            if (message.Type is "error" or "warning" && !message.Text.Contains("negotiation") && !message.Text.Contains("signalR"))
            {
                Record(_console, $"[{message.Type}] {message.Text}");
            }
        };

        page.Dialog += (_, dialog) =>
        {
            Record(_dialogs, $"[{dialog.Type}] {dialog.Message}");
            _ = dialog.AcceptAsync();
        };

        page.RequestFailed += (_, request) => Record(_failedRequests, $"{request.Method} {request.Url}: {request.Failure}");

        page.Response += (_, response) => _ = RecordApiErrorAsync(response);
    }

    public IPage Page { get; }

    /// <summary>Every API response of 400 and above seen so far, for tests that assert the app stayed clean.</summary>
    public IReadOnlyList<string> ApiErrors
    {
        get
        {
            lock (_apiErrors)
            {
                return [.. _apiErrors];
            }
        }
    }

    /// <summary>Runs the test body; a failure is rethrown with the app's own account of what happened.</summary>
    public async Task RunAsync(string name, Func<IPage, Task> body)
    {
        try
        {
            await body(Page);
        }
        catch (Exception exception)
        {
            var report = await ReportAsync(name);
            throw new VCShellAppTestException($"{exception.Message}{Environment.NewLine}{report}", exception);
        }
    }

    private async Task<string> ReportAsync(string name)
    {
        var report = new StringBuilder();
        var directory = VCShellAppEnvironment.DiagnosticsDirectory;

        try
        {
            Directory.CreateDirectory(directory);
            var screenshot = Path.Combine(directory, $"{name}.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshot, FullPage = true });
            report.AppendLine($"Screenshot: {screenshot}");
        }
        catch (Exception screenshotError)
        {
            report.AppendLine($"Screenshot failed: {screenshotError.Message}");
        }

        report.AppendLine($"URL: {Page.Url}");
        report.AppendLine($"Title: {await SafeAsync(() => Page.TitleAsync())}");
        report.AppendLine($"Body (start): {Truncate(await SafeAsync(() => Page.InnerTextAsync("body")), 800)}");
        Append(report, "API errors", _apiErrors);
        Append(report, "Dialogs", _dialogs);
        Append(report, "Failed requests", _failedRequests);
        Append(report, "Console", _console);

        return report.ToString();
    }

    private async Task RecordApiErrorAsync(IResponse response)
    {
        try
        {
            if (response.Status < 400)
            {
                return;
            }

            var path = new Uri(response.Url).AbsolutePath;
            if (!path.StartsWith("/api/") && !path.StartsWith("/connect/") && !path.StartsWith("/revoke/"))
            {
                return;
            }

            var body = await response.TextAsync();
            Record(_apiErrors, $"{response.Request.Method} {path} -> {response.Status}: {Truncate(body, 400)}");
        }
        catch
        {
            // Diagnostics only.
        }
    }

    private static void Record(List<string> target, string line)
    {
        lock (target)
        {
            target.Add(line);
        }
    }

    private static void Append(StringBuilder report, string title, List<string> lines)
    {
        lock (lines)
        {
            report.AppendLine($"{title}: {(lines.Count == 0 ? "none" : string.Empty)}");
            foreach (var line in lines.Take(20))
            {
                report.AppendLine($"  {line}");
            }
        }
    }

    private static async Task<string> SafeAsync(Func<Task<string>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception exception)
        {
            return $"<{exception.Message}>";
        }
    }

    private static string Truncate(string value, int length)
        => value == null ? string.Empty : value.Length <= length ? value : value[..length] + "…";

    public async ValueTask DisposeAsync() => await _context.CloseAsync();
}

internal sealed class VCShellAppTestException(string message, Exception inner) : Exception(message, inner);
