using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using OpenIddict.Abstractions;
using VirtoCommerce.CustomerModule.Data.OpenIddict;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// What the VC-Shell app needs from the backend, split by where it is registered: harness overrides (claim
/// producers, permission policies), web-host services (the module's own REST controllers hosted as they are, bearer
/// authentication for the framework's token) and the pipeline (the built app under its base path, the replica
/// platform endpoints, the token endpoints).
/// </summary>
internal static class VCShellServices
{
    public const string AppId = "vc-sales-rep";
    public const string AppPath = "/apps/" + AppId;

    /// <summary>Applied as the harness's last-wins overrides, so the component tests keep their own defaults.</summary>
    public static void AddVCShellOverrides(IServiceCollection services)
    {
        // Same identity claim types and REAL claim producers as the storefront host: the token carries sub/name/role/
        // permission/memberId exactly as /connect/token mints them.
        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
            options.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
            options.ClaimsIdentity.RoleClaimType = OpenIddictConstants.Claims.Role;
            options.ClaimsIdentity.EmailClaimType = OpenIddictConstants.Claims.Email;
        });
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomUserClaimsPrincipalFactory>();
        services.AddTransient<ITokenClaimProvider, OrganizationIdClaimProvider>();
        services.AddSingleton<TestTokenService>();

        // [Authorize("permission")] on the module controllers, decided the platform's way.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, DefaultPermissionAuthorizationHandler>();

        // No dynamic properties in the harness; the JSON contract and anything else asking get "none".
        services.AddSingleton<IDynamicPropertyMetaDataResolver>(EmptyDynamicPropertyMetaDataResolver.Instance);
    }

    /// <summary>Web-host-only registrations: MVC over the module's controllers, cookie authentication.</summary>
    public static void AddWebServices(IServiceCollection services)
    {
        var moduleWeb = typeof(SalesRepController).Assembly;

        services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                if (!manager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == moduleWeb))
                {
                    manager.ApplicationParts.Add(new AssemblyPart(moduleWeb));
                }
            })
            .AddNewtonsoftJson(PlatformJson.Configure);

        // The framework sends no bearer header: it signs in through /api/platform/security/login and every call
        // carries the Identity application cookie, exactly as against the platform. AddIdentity (copied in with the
        // harness) already made that cookie the default scheme; API callers get 401/403 instead of redirects.
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddDataProtection().UseEphemeralDataProtectionProvider();
    }

    public static void ConfigurePipeline(WebApplication app, string appDirectory)
    {
        // What the platform's ApiErrorWrappingMiddleware does: an unhandled exception answers 500 with its message,
        // which the session recorder then carries into the failing test's report.
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync($"{exception.GetType().Name}: {exception.Message}");
            }
        });

        // The built app exactly as the platform serves Content/<app-id>: static files under /apps/<app-id>/.
        var appFiles = new PhysicalFileProvider(appDirectory);
        app.UseStaticFiles(new StaticFileOptions { FileProvider = appFiles, RequestPath = AppPath });

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        TestTokenEndpoints.Map(app);
        VCShellPlatformEndpoints.Map(app);
        app.MapGet("/health", () => Results.Text("Healthy"));

        // The app is a SPA with history-mode routes under its base path: deep links land on index.html.
        app.MapFallbackToFile(AppPath + "/{*path:nonfile}", "index.html", new StaticFileOptions { FileProvider = appFiles });
    }
}
