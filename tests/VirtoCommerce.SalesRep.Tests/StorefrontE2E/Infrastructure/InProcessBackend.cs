using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GraphQL.Types;
using Microsoft.Extensions.Logging;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The component harness hosted as a real HTTP backend inside the test process: the same
/// <see cref="SalesRepTestContext"/> (in-memory SQLite databases, RAM Lucene, hand-written doubles) behind Kestrel on
/// a random loopback port, so the real storefront can talk to it while the tests seed and assert through the very
/// same DbContexts. No second dotnet process, nothing read from <c>back/modules</c>.
/// </summary>
internal sealed class InProcessBackend : IAsyncDisposable
{
    public const string StoreId = "B2B-store";

    private readonly WebApplication _app;

    private InProcessBackend(WebApplication app, SalesRepTestContext context, string url)
    {
        _app = app;
        Context = context;
        Url = url;
    }

    /// <summary>The harness: seed, index and assert through it exactly as the component tests do.</summary>
    public SalesRepTestContext Context { get; }

    /// <summary>Loopback base URL of the Kestrel host (<c>http://127.0.0.1:port</c>), what the storefront proxies to.</summary>
    public string Url { get; }

    public TestTokenService Tokens => Context.GetRequiredService<TestTokenService>();

    public static async Task<InProcessBackend> StartAsync()
    {
        WebApplication app = null;

        var context = SalesRepTestContext.Create(
            configureOverrides: StorefrontServices.AddStorefrontShell,
            providerFactory: services =>
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = "VirtoCommerce.SalesRep.StorefrontE2E" });
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
                builder.WebHost.UseUrls("http://127.0.0.1:0");

                // The harness registers its own IConfiguration (the order full-text flag). The host already owns one,
                // so the key moves into the host configuration and the harness descriptor stays out.
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string> { ["Search:OrderFullTextSearchEnabled"] = "true" });

                foreach (var descriptor in services.Where(x => x.ServiceType != typeof(IConfiguration)))
                {
                    builder.Services.Add(descriptor);
                }

                StorefrontServices.AddWebServices(builder.Services);

                app = builder.Build();
                StorefrontServices.ConfigurePipeline(app);

                // Not started yet: the harness still wires event handlers and search builders on this provider.
                return app.Services;
            });

        StorefrontServices.ConfigureStoreDouble(context);

        // Eagerly, so a graph type whose dependency is missing fails here with the DI message, not as an opaque
        // "Error executing document" on the storefront's first request.
        app.Services.GetRequiredService<ISchema>().Initialize();

        await app.StartAsync();

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        var backend = new InProcessBackend(app, context, url);

        await backend.SelfCheckAsync();

        return backend;
    }

    /// <summary>
    /// The storefront's first request, made here first: a failure in the shell wiring then reads as a GraphQL error
    /// in the test output instead of a blank storefront page.
    /// </summary>
    private async Task SelfCheckAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(Url) };
        var response = await client.PostAsJsonAsync("/graphql", new
        {
            query = "{ store(domain: \"127.0.0.1\") { storeId defaultLanguage { cultureName } settings { modules { moduleId settings { name value } } } } }",
        });
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode || json.Contains("\"errors\"") || !json.Contains($"\"storeId\":\"{StoreId}\""))
        {
            throw new InvalidOperationException($"The in-process backend does not answer the storefront's store query ({(int)response.StatusCode}): {json}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();

        // The provider was the app's and is gone; this closes the in-memory SQLite connections.
        Context.Dispose();
    }
}
