using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The real storefront (vc-theme-b2b-vue) as its own Vite dev server, pointed at the in-process backend through the
/// theme's own proxy: <c>APP_BACKEND_URL</c> in the process environment wins over <c>.env.local</c>. One per test
/// group; killed with its whole process tree on dispose.
/// </summary>
internal sealed class ViteDevServer : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output;

    private ViteDevServer(Process process, StringBuilder output, string url)
    {
        _process = process;
        _output = output;
        Url = url;
    }

    /// <summary><c>https://localhost:port</c> (the theme's dev server is HTTPS through vite-plugin-mkcert).</summary>
    public string Url { get; }

    /// <summary>Everything the dev server printed so far, for failure diagnostics.</summary>
    public string Output
    {
        get
        {
            lock (_output)
            {
                return _output.ToString();
            }
        }
    }

    /// <summary>The vc-frontend checkout (see <see cref="StorefrontAvailability"/>), or an exception naming what is missing.</summary>
    public static string ResolveFrontendDirectory() => StorefrontAvailability.FrontendDirectory;

    public static async Task<ViteDevServer> StartAsync(string frontendDirectory, string backendUrl, string logDirectory = null, TimeSpan? timeout = null)
    {
        var port = GetFreePort();
        var logFile = logDirectory == null ? null : Path.Combine(logDirectory, $"vite-{port}.log");
        if (logFile != null)
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Vite's own entry point under node, not `yarn dev`: a cmd -> yarn -> node chain leaves the dev server as an
        // orphan when the chain is killed, and its inherited stdout pipe then keeps the exit wait hanging.
        var viteEntry = Path.Combine(frontendDirectory, "node_modules", "vite", "bin", "vite.js");
        var startInfo = new ProcessStartInfo("node", $"\"{viteEntry}\" --port {port} --strictPort");

        startInfo.WorkingDirectory = frontendDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.Environment["APP_BACKEND_URL"] = backendUrl;
        startInfo.Environment["BROWSER"] = "none";

        var output = new StringBuilder();
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("yarn did not start.");

        process.OutputDataReceived += (_, e) => Append(output, logFile, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, logFile, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var url = $"https://localhost:{port}";
        var server = new ViteDevServer(process, output, url);

        try
        {
            await server.WaitUntilReadyAsync(timeout ?? TimeSpan.FromMinutes(3));
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }

        return server;
    }

    private async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"The Vite dev server exited with code {_process.ExitCode}:\n{Output}");
            }

            try
            {
                using var response = await client.GetAsync(Url + "/");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException)
            {
                // Still compiling; try again.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"The Vite dev server did not answer on {Url} within {timeout}:\n{Output}");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Append(StringBuilder output, string logFile, string line)
    {
        if (line == null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);

            if (logFile != null)
            {
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            // The dev server and its helpers (esbuild, checker workers), or they outlive the test run.
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            // Bounded: a helper that inherited the output pipes could otherwise keep this wait open.
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await _process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (OperatingSystem.IsWindows())
                {
                    using var killer = Process.Start(new ProcessStartInfo("taskkill", $"/T /F /PID {_process.Id}") { CreateNoWindow = true, UseShellExecute = false });
                    killer?.WaitForExit(5000);
                }
            }
        }

        _process.Dispose();
    }
}
