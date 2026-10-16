using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// The component harness hosted as the VC-Shell app's backend inside the test process: the same
/// <see cref="SalesRepTestContext"/> (in-memory SQLite databases, RAM Lucene, hand-written doubles) behind Kestrel
/// on a random loopback port, serving the module's REST controllers and the built VC-Shell app from the same
/// origin, so no dev server or proxy sits between the browser and the harness.
/// </summary>
internal sealed class InProcessVCShellBackend : IAsyncDisposable
{
    public const string StoreId = "B2B-store";

    private readonly WebApplication _app;
    private readonly SemaphoreSlim _gate;

    private InProcessVCShellBackend(WebApplication app, SemaphoreSlim gate, SalesRepTestContext context, string url)
    {
        _app = app;
        _gate = gate;
        Context = context;
        Url = url;
    }

    /// <summary>The harness: seed, index and assert through it exactly as the component tests do.</summary>
    public SalesRepTestContext Context { get; }

    /// <summary>Loopback base URL of the Kestrel host (<c>http://127.0.0.1:port</c>).</summary>
    public string Url { get; }

    /// <summary>Where the app's index.html is served (<c>/apps/vc-sales-rep/</c>), what the browser opens.</summary>
    public string AppUrl => Url + VCShellServices.AppPath + "/";

    /// <summary>
    /// Runs test-side work against the harness between requests. Its in-memory SQLite databases each live on one
    /// connection, which is not thread-safe, so the host answers one request at a time and a test's reads take the
    /// same turn: the blade may still be refreshing after the save or block it waited for.
    /// </summary>
    public async Task<T> RunExclusiveAsync<T>(Func<SalesRepTestContext, Task<T>> work)
    {
        await _gate.WaitAsync();
        try
        {
            return await work(Context);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task<InProcessVCShellBackend> StartAsync(string appDirectory)
    {
        WebApplication app = null;
        var gate = new SemaphoreSlim(1, 1);

        var context = SalesRepTestContext.Create(
            configureOverrides: VCShellServices.AddVCShellOverrides,
            providerFactory: services =>
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    // MVC loads the assembly of this name as its first application part: the test assembly itself.
                    ApplicationName = typeof(InProcessVCShellBackend).Assembly.GetName().Name,
                });
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
                builder.WebHost.UseUrls("http://127.0.0.1:0");

                // The harness registers its own IConfiguration (the order full-text flag). The host already owns one,
                // so the key moves into the host configuration and the harness descriptor stays out.
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string> { ["Search:OrderFullTextSearchEnabled"] = "true" });

                foreach (var descriptor in services.Where(x => x.ServiceType != typeof(IConfiguration)))
                {
                    builder.Services.Add(descriptor);
                }

                VCShellServices.AddWebServices(builder.Services);

                app = builder.Build();
                VCShellServices.ConfigurePipeline(app, appDirectory, gate);

                // Not started yet: the harness still wires event handlers and search builders on this provider.
                return app.Services;
            });

        // The store select of the details blade shows this name.
        context.GetRequiredService<TestServicesConfiguration.TestStoreService>().Customize = store => store.Name = "E2E store";

        // What Startup.Configure does once per process: the JSON contract consults this static when it serializes a
        // member's dynamic properties.
        DynamicPropertyMetadata.Initialize(EmptyDynamicPropertyMetaDataResolver.Instance);

        await app.StartAsync();

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        var backend = new InProcessVCShellBackend(app, gate, context, url);

        await backend.SelfCheckAsync();

        return backend;
    }

    /// <summary>The app's first two requests, made here first: a wiring fault then reads as a message, not a blank page.</summary>
    private async Task SelfCheckAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(Url) };

        var index = await client.GetAsync(VCShellServices.AppPath + "/");
        var html = await index.Content.ReadAsStringAsync();
        if (!index.IsSuccessStatusCode || !html.Contains("<script"))
        {
            throw new InvalidOperationException($"The in-process backend does not serve the VC-Shell app ({(int)index.StatusCode}): {html[..Math.Min(html.Length, 300)]}");
        }

        var anonymous = await client.GetAsync("/api/platform/security/currentuser");
        if (anonymous.StatusCode != HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException($"An anonymous current-user request answered {(int)anonymous.StatusCode}; cookie authentication is not in front of the API.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();

        // The provider was the app's and is gone; this closes the in-memory SQLite connections.
        Context.Dispose();
        _gate.Dispose();
    }
}
