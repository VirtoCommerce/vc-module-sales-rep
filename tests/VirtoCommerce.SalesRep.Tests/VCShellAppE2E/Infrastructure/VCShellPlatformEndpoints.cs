using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// The platform and module endpoints the app calls that no referenced package implements (their controllers live
/// in non-packable .Web projects): sign-in and sign-out, the current-user, notifications and apps calls the shell
/// makes at boot, the customization and provider calls of the sign-in page, and the store and organization
/// searches the details blade fills its selects from. Thin replicas over the harness services, serialized with
/// the platform's JSON contract.
/// </summary>
internal static class VCShellPlatformEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/platform/security/login", LoginAsync);
        app.MapGet("/api/platform/security/logout", LogoutAsync);
        app.MapGet("/api/platform/security/currentuser", CurrentUserAsync).RequireAuthorization();
        app.MapPost("/api/platform/pushnotifications", () => PlatformJson.Result(new { totalCount = 0, notifyEvents = Array.Empty<object>() })).RequireAuthorization();
        app.MapGet("/api/platform/apps", () => PlatformJson.Result(Array.Empty<object>()));
        app.MapGet("/api/platform/settings/ui/customization", () => PlatformJson.Result(new { name = "VirtoCommerce.Platform.UI.Customization", valueType = "Json", value = "{}", defaultValue = "{}" }));
        app.MapGet("/externalsignin/providers", () => PlatformJson.Result(Array.Empty<object>()));
        app.MapPost("/api/stores/search", SearchStoresAsync).RequireAuthorization();
        app.MapPost("/api/organizations/search", SearchOrganizationsAsync).RequireAuthorization();

        // Test-only: a browser context signs in as a seeded account without its password; the Identity cookie
        // comes back on the response, exactly as the sign-in page would obtain it.
        app.MapPost("/e2e/sign-in", SignInAsync);
    }

    // SecurityController.Login: the password sign-in that sets the Identity application cookie.
    private static async Task<IResult> LoginAsync(HttpContext http, SignInManager<ApplicationUser> signInManager)
    {
        var request = await PlatformJson.ReadAsync<LoginRequest>(http.Request);
        var result = await signInManager.PasswordSignInAsync(request?.UserName ?? string.Empty, request?.Password ?? string.Empty, isPersistent: true, lockoutOnFailure: true);

        return PlatformJson.Result(new { succeeded = result.Succeeded, isLockedOut = result.IsLockedOut, isNotAllowed = result.IsNotAllowed, requiresTwoFactor = result.RequiresTwoFactor });
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> SignInAsync(string userId, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        var user = await userManager.FindByIdAsync(userId ?? string.Empty);
        if (user == null)
        {
            return Results.NotFound();
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.NoContent();
    }

    // SecurityController.GetCurrentUser's UserDetail, read off the principal the real claim producers built.
    private static async Task<IResult> CurrentUserAsync(HttpContext http, UserManager<ApplicationUser> userManager)
    {
        var principal = http.User;
        var user = await userManager.FindByIdAsync(principal.FindFirstValue(OpenIddictConstants.Claims.Subject) ?? string.Empty);
        if (user == null)
        {
            return Results.Unauthorized();
        }

        return PlatformJson.Result(new
        {
            id = user.Id,
            userName = user.UserName,
            email = user.Email,
            userType = user.UserType,
            memberId = user.MemberId,
            isAdministrator = principal.IsInRole(PlatformConstants.Security.SystemRoles.Administrator),
            permissions = principal.FindAll(PlatformConstants.Security.Claims.PermissionClaimType).Select(claim => claim.Value).Distinct().ToArray(),
            passwordExpired = false,
            daysTillPasswordExpiry = 365,
            authenticationMethod = "Password",
            isSsoAuthenticationMethod = false,
            canAccessAdminUI = true,
        });
    }

    // StoreModuleController.SearchStores: the harness store double is the only store there is.
    private static async Task<IResult> SearchStoresAsync(IStoreService storeService)
    {
        var store = await storeService.GetByIdAsync(InProcessVCShellBackend.StoreId);
        return PlatformJson.Result(new StoreSearchResult { TotalCount = 1, Results = [store] });
    }

    // CustomerModuleController.SearchOrganizations over the real, database-backed member search service.
    private static async Task<IResult> SearchOrganizationsAsync(HttpContext http, IMemberSearchService memberSearchService)
    {
        var criteria = await PlatformJson.ReadAsync<MembersSearchCriteria>(http.Request) ?? AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        criteria.MemberType = typeof(Organization).Name;
        criteria.MemberTypes = null;

        var found = await memberSearchService.SearchMembersAsync(criteria);

        return PlatformJson.Result(new OrganizationSearchResult
        {
            TotalCount = found.TotalCount,
            Results = found.Results.OfType<Organization>().ToList(),
        });
    }

    private sealed record LoginRequest(string UserName, string Password);
}
