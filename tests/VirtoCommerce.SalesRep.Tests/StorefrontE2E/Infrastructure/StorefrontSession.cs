using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// A signed-in browser context and its page. Records what the storefront said while the test ran (console errors,
/// dialogs, GraphQL operations that returned errors), and on a failure folds that plus a screenshot into the
/// exception, so a red test explains itself without re-running under a debugger.
/// </summary>
internal sealed class StorefrontSession : IAsyncDisposable
{
    private readonly IBrowserContext _context;
    private readonly List<string> _console = [];
    private readonly List<string> _dialogs = [];
    private readonly List<string> _graphQlErrors = [];
    private readonly List<string> _failedRequests = [];

    public StorefrontSession(IBrowserContext context, IPage page)
    {
        _context = context;
        Page = page;

        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
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

        page.Response += (_, response) => _ = RecordGraphQlErrorsAsync(response);
    }

    public IPage Page { get; }

    /// <summary>Runs the test body; a failure is rethrown with the storefront's own account of what happened.</summary>
    public async Task RunAsync(string name, Func<IPage, Task> body)
    {
        try
        {
            await body(Page);
        }
        catch (Exception exception)
        {
            var report = await ReportAsync(name);
            throw new StorefrontTestException($"{exception.Message}{Environment.NewLine}{report}", exception);
        }
    }

    private async Task<string> ReportAsync(string name)
    {
        var report = new StringBuilder();
        var directory = Path.Combine(AppContext.BaseDirectory, "StorefrontE2E-diagnostics");

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
        Append(report, "GraphQL errors", _graphQlErrors);
        Append(report, "Dialogs", _dialogs);
        Append(report, "Failed requests", _failedRequests);
        Append(report, "Console", _console);

        return report.ToString();
    }

    private async Task RecordGraphQlErrorsAsync(IResponse response)
    {
        try
        {
            if (!response.Url.Contains("/graphql") || response.Request.Method != "POST")
            {
                return;
            }

            var body = await response.TextAsync();
            if (!body.Contains("\"errors\""))
            {
                return;
            }

            var operation = "?";
            if (response.Request.PostData is { } postData)
            {
                using var document = JsonDocument.Parse(postData);
                operation = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("operationName", out var op)
                    ? op.GetString()
                    : "?";
            }

            Record(_graphQlErrors, $"{operation} ({(int)response.Status}): {Truncate(body, 600)}");
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

internal sealed class StorefrontTestException(string message, Exception inner) : Exception(message, inner);
