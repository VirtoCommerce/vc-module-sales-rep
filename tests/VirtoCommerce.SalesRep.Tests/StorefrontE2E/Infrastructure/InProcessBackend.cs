using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The component harness hosted as a real HTTP backend inside the test process: the same
/// <see cref="SalesRepTestContext"/> (the module and the platform around it, as they start up, over in-memory SQLite)
/// behind Kestrel on a loopback port, with the modules the storefront shell queries (<see cref="StorefrontShellModules"/>),
/// so the real storefront talks to it while the tests seed and assert through the very same services and databases.
/// No second dotnet process, nothing read from <c>back/modules</c>.
/// </summary>
internal sealed class InProcessBackend : IAsyncDisposable
{
    public const string StoreId = SalesRepTestContext.DefaultStoreId;

    private readonly WebApplication _app;
    private readonly RequestGate _gate;

    private InProcessBackend(WebApplication app, RequestGate gate, SalesRepTestContext context, string url)
    {
        _app = app;
        _gate = gate;
        Context = context;
        Url = url;
    }

    /// <summary>The harness: seed, index and assert through it exactly as the component tests do.</summary>
    public SalesRepTestContext Context { get; }

    /// <summary>
    /// Loopback base URL of the Kestrel host (<c>http://127.0.0.1:port</c>): what a clone's Vite dev server proxies
    /// to, and where a built storefront is served.
    /// </summary>
    public string Url { get; }

    public TestTokenService Tokens => Context.GetRequiredService<TestTokenService>();

    /// <summary>
    /// Runs test-side work against the harness between requests. Its in-memory SQLite databases each live on one
    /// connection, which is not thread-safe, so the host answers one request at a time and a test's reads take the
    /// same turn: the page may still be refetching after it confirms a save.
    /// </summary>
    public Task<T> RunExclusiveAsync<T>(Func<SalesRepTestContext, Task<T>> work) => _gate.RunAsync(() => work(Context));

    /// <param name="storefrontBuild">A built storefront to serve from the backend's own origin; null when there is none to serve.</param>
    /// <param name="withTaskManagement">Whether VirtoCommerce.TaskManagement is installed: the module and its place in the module list.</param>
    /// <param name="port">The loopback port to listen on; 0 takes any free one.</param>
    public static async Task<InProcessBackend> StartAsync(string storefrontBuild = null, bool withTaskManagement = true, int port = 0)
    {
        WebApplication app = null;
        var gate = new RequestGate();

        var context = SalesRepTestContext.Create(
            configureOverrides: StorefrontServices.AddStorefrontShell,
            withTaskManagement: withTaskManagement,
            buildHost: services =>
            {
                // Development, as a platform runs locally: X-API then puts a failing resolver's exception in the response,
                // and so in the test output.
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ApplicationName = "VirtoCommerce.SalesRep.StorefrontE2E",
                    EnvironmentName = Environments.Development,
                });
                builder.Logging.SetMinimumLevel(LogLevel.Warning);
                builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

                // A Development platform validates its container at start; so does this host, for every service it
                // registers. Not the lifetimes: the module captures a scoped service in a singleton through X-Cart's
                // wishlist authorization (scoped ISalesRepRoleResolver <- SalesRepCustomerCartSharingScopePolicy <-
                // CartSharingService <- singleton CanAccessCartAuthorizationHandler), which a Development platform
                // refuses to start with. A production one starts with it, as this host does. A known issue: VCST-6476
                // (vc-module-sales-rep#29, not merged yet) makes the resolver a singleton; turn ValidateScopes on once
                // this branch has it.
                builder.Host.UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = true;
                    options.ValidateScopes = false;
                });

                // The host's own configuration, with what the platform's appsettings.json sets that the hosted code reads.
                builder.Configuration.AddInMemoryCollection(SalesRepTestContext.ConfigurationValues);

                foreach (var descriptor in services)
                {
                    builder.Services.Add(descriptor);
                }

                StorefrontServices.AddWebServices(builder.Services);

                app = builder.Build();
                StorefrontServices.ConfigurePipeline(app, gate, storefrontBuild);

                // Not started yet: the platform's and the modules' startup still adds their endpoints to it.
                return app;
            });

        await StorefrontShellModules.PostInitializeAsync(app);
        await StorefrontServices.ConfigureStoreAsync(context);

        // Eagerly, so a graph type whose dependency is missing fails here with the DI message, not as an opaque
        // "Error executing document" on the storefront's first request.
        app.Services.GetRequiredService<ISchema>().Initialize();

        await app.StartAsync();

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        var backend = new InProcessBackend(app, gate, context, url);

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
        var response = await GraphQlHttp.PostAsync(client, new
        {
            query = "{ store(domain: \"127.0.0.1\") { storeId defaultLanguage { cultureName } settings { modules { moduleId settings { name value } } } plugins(appId: \"vc-frontend\") { id } } }",
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

        // The provider was the app's: its databases went with it; this removes the harness's assets folder.
        Context.Dispose();
        _gate.Dispose();
    }
}
