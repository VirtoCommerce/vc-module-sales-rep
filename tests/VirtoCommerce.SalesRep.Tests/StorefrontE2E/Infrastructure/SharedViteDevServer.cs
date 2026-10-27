using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

[assembly: AssemblyFixture(typeof(VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.SharedViteDevServer))]

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The run's one Vite dev server for a vc-frontend clone. Vite compiles the storefront on first request and keeps what
/// it compiled, so only the run's first page load pays for that, not each group's. Its proxy target is fixed when it
/// starts, so every group's backend listens on <see cref="BackendPort"/>, one group at a time. The first group that
/// needs it starts it; xunit disposes this assembly fixture, and the dev server with it, once every test has run.
/// </summary>
public sealed class SharedViteDevServer : IAsyncDisposable
{
    private static readonly Lazy<int> _backendPort = new(ViteDevServer.GetFreePort);
    private static readonly SemaphoreSlim _start = new(1, 1);
    private static ViteDevServer _server;

    /// <summary>The loopback port each group's backend takes in turn while a clone runs, picked once per run.</summary>
    internal static int BackendPort => _backendPort.Value;

    /// <summary>The run's dev server, started on first use. One that has died fails the group with its output.</summary>
    internal static async Task<ViteDevServer> GetAsync(string frontendDirectory, string logDirectory)
    {
        await _start.WaitAsync();
        try
        {
            if (_server == null)
            {
                _server = await ViteDevServer.StartAsync(frontendDirectory, $"http://127.0.0.1:{BackendPort}", logDirectory);
            }
            else if (_server.HasExited)
            {
                throw new InvalidOperationException($"The run's Vite dev server has exited:\n{_server.Output}");
            }

            return _server;
        }
        finally
        {
            _start.Release();
        }
    }

    public ValueTask DisposeAsync() => StopAsync();

    private static async ValueTask StopAsync()
    {
        if (_server != null)
        {
            await _server.DisposeAsync();
            _server = null;
        }
    }
}
