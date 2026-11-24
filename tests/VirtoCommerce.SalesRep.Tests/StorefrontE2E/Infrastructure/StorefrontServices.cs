using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using PushMessagesSettings = VirtoCommerce.PushMessages.Core.ModuleConstants.Settings.General;
using SalesRepSettings = VirtoCommerce.SalesRep.Core.ModuleConstants.Settings.General;
using StoreSettings = VirtoCommerce.StoreModule.Core.ModuleConstants.Settings.General;
using XapiSettings = VirtoCommerce.Xapi.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// What the real storefront needs from the backend beyond the platform and the modules the harness hosts
/// (<see cref="PlatformModules"/>), split by where it is registered: harness services, web-host services
/// (authentication, the storefront's files) and the pipeline before the modules' GraphQL endpoints; and the store as
/// an administrator sets it up for the hub.
/// </summary>
internal static class StorefrontServices
{
    public const string DefaultCulture = "en-US";

    /// <summary>The platform's paths: what a clone's dev proxy forwards to the backend (vc-frontend's vite.config.ts).</summary>
    private static readonly string[] _platformPaths =
        ["/api", "/graphql", "/ucp", "/connect", "/revoke", "/.well-known", "/cms-content", "/modules", "/externalsignin", "/signin-oidc", "/signin-google"];

    /// <summary>Added after the platform's and the module's registrations.</summary>
    public static void AddStorefrontShell(IServiceCollection services)
    {
        services.AddSingleton<TestTokenService>();
        services.AddStorefrontShellModules();
    }

    /// <summary>Web-host-only registrations: the platform's Kestrel options, bearer authentication for the storefront's token.</summary>
    public static void AddWebServices(IServiceCollection services)
    {
        // As Platform.Web's Startup: X-API's Newtonsoft serializer reads a request's body synchronously.
        services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(ValidateTestToken);

        // ASP.NET Identity makes its cookie the default scheme; the platform sets its own back, as this sets the token's.
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });
    }

    private static void ValidateTestToken(JwtBearerOptions options)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = TestTokenService.Issuer,
            ValidAudience = TestTokenService.Audience,
            IssuerSigningKey = TestTokenService.SigningKey,
            NameClaimType = OpenIddictConstants.Claims.Name,
            RoleClaimType = OpenIddictConstants.Claims.Role,
        };
    }

    /// <summary>
    /// The request pipeline up to the modules' endpoints, which their startup adds after it, as the platform's does: the
    /// plugin's and the storefront's files, the gate, authentication; then the health check and the token endpoints.
    /// </summary>
    public static void ConfigurePipeline(WebApplication app, RequestGate gate, string storefrontBuild)
    {
        // The hub plugin's build, on the platform's route for it; the storefront's dev proxy forwards that path here.
        // Without a build there is nothing to serve, and the groups that need one say so (StorefrontPlugin).
        if (Directory.Exists(StorefrontPlugin.BuildDirectory))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(StorefrontPlugin.BuildDirectory),
                RequestPath = StorefrontPlugin.RequestPath,
            });
        }

        // A built storefront, served from the backend's own origin as the cloud serves it: its files, and index.html
        // for a page navigation to one of its routes. The platform's paths never fall back, so a missing endpoint
        // stays a 404 as it would behind the dev proxy.
        if (storefrontBuild != null)
        {
            var storefrontFiles = new PhysicalFileProvider(storefrontBuild);
            app.Use((context, next) =>
            {
                if (IsStorefrontRoute(context.Request) && !storefrontFiles.GetFileInfo(context.Request.Path.Value).Exists)
                {
                    context.Request.Path = "/index.html";
                }

                return next(context);
            });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = storefrontFiles, ServeUnknownFileTypes = true });
        }

        // Everything past the static files takes its turn at the gate: the storefront sends its queries in parallel.
        gate.Use(app);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Text("Healthy"));
        TestTokenEndpoints.Map(app);
    }

    /// <summary>
    /// The store as an administrator sets it up for the hub, through the store and settings services: one language,
    /// the hub on (a public store setting the storefront reads from store.settings.modules), guests allowed, push
    /// messages off; and the platform setting that makes X-API report every installed module's version in that list,
    /// which is how the storefront learns task management is installed.
    /// </summary>
    /// <remarks>
    /// Push messages are on in every store by default. The storefront then subscribes to them over a WebSocket, which
    /// lives as long as the page, outside the gate that keeps the in-memory databases to one caller at a time
    /// (<see cref="RequestGate"/>): its operations ran alongside the page's requests and stalled them. The hub has no
    /// part in push messages, so the store has them off, as an administrator can turn them off.
    /// </remarks>
    public static async Task ConfigureStoreAsync(SalesRepTestContext context)
    {
        await context.SetSettingAsync(XapiSettings.ReturnModuleVersion, true);

        await context.SaveStoreAsync(InProcessBackend.StoreId, store =>
        {
            store.Name = "E2E store";
            store.Languages = [DefaultCulture];
            store.DefaultLanguage = DefaultCulture;
            store.Settings.Single(x => x.Name == SalesRepSettings.SalesRepEnabled.Name).Value = true;
            store.Settings.Single(x => x.Name == StoreSettings.AllowAnonymousUsers.Name).Value = true;
            store.Settings.Single(x => x.Name == PushMessagesSettings.Enable.Name).Value = false;
        });
    }

    /// <summary>A page navigation (a GET for HTML) outside the platform's paths.</summary>
    private static bool IsStorefrontRoute(HttpRequest request)
        => (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
           && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
           && !_platformPaths.Any(x => request.Path.StartsWithSegments(x, StringComparison.OrdinalIgnoreCase));
}
