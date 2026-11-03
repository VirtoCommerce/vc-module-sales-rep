using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.SalesReps;

/// <summary>
/// The data every test of the group sees: two organizations a rep can serve, the role the details blade offers, an
/// administrator who signs in to the app, and one existing rep (Rita) for the tests that act on a rep.
/// </summary>
public sealed class SalesRepsEnvironment : VCShellAppEnvironment
{
    public const string FabrikamId = "e2e-org-fabrikam";
    public const string ContosoId = "e2e-org-contoso";
    public const string AdminEmail = "admin.e2e@e2e.local";
    public const string RitaEmail = "rita.rep@e2e.local";

    public string AdminUserId { get; private set; }

    public SalesRepDetails Rita { get; private set; }

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(FabrikamId, organization => organization.Name = "Fabrikam");
        await context.SeedOrganizationAsync(ContosoId, organization => organization.Name = "Contoso");
        await context.CreateGrantingRoleAsync("Sales Rep");

        AdminUserId = await CreateAdministratorAsync(context, AdminEmail);
        Rita = await context.CreateRepInStoreAsync("Rita", "Rep", RitaEmail, InProcessVCShellBackend.StoreId, FabrikamId);

        // Member searches with a type filter go to the index, as on the platform.
        await context.IndexMembersAsync(FabrikamId, ContosoId, Rita.Id);
    }

    // The platform's administrator: the IsAdministrator flag, which the claims factory turns into the
    // "__administrator" system role claim that every permission check lets through.
    private static async Task<string> CreateAdministratorAsync(SalesRepTestContext context, string email)
    {
        using var userManager = context.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        var user = AbstractTypeFactory<ApplicationUser>.TryCreateInstance();
        user.UserName = email;
        user.Email = email;
        user.UserType = "Manager";
        user.IsAdministrator = true;

        Ensure(await userManager.CreateAsync(user, "P@ssw0rd123!"));

        return user.Id;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
