using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Validates the make-or-break slice of the "real services" harness: VC's custom Identity stack running on
/// in-memory SQLite. Specifically that role assignment persists via <c>user.Roles</c> (the custom store) —
/// which is exactly what SalesRepService relies on. If this is green, Option B is viable.
/// </summary>
[Trait("Category", "Component")]
public class SecuritySliceTests
{
    [Fact]
    public async Task RealUserManager_CreatesUserWithRole_PersistsRoleViaUserRoles()
    {
        using var connection = SqliteTestDbContextFactory.CreateConnection();
        var options = SqliteTestDbContextFactory.CreateOptions<SecurityDbContext>(connection);

        var services = new ServiceCollection().AddSecuritySlice(options);
        using var provider = services.BuildServiceProvider();

        // Arrange: a granting role + a user holding it (assigned via user.Roles, as SalesRepService does)
        using (var scope = provider.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var role = AbstractTypeFactory<Role>.TryCreateInstance();
            role.Id = "sales-rep-role";
            role.Name = "Sales Representative";
            role.Permissions = [new Permission { Name = "sales-rep:access" }];
            (await roleManager.CreateAsync(role)).Succeeded.Should().BeTrue();

            var user = AbstractTypeFactory<ApplicationUser>.TryCreateInstance();
            user.UserName = "rep@test.com";
            user.Email = "rep@test.com";
            user.MemberId = "member-1";
            user.Roles = [role];
            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert: a fresh scope/manager reads the user back with the role assignment intact
        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var search = scope.ServiceProvider.GetRequiredService<VirtoCommerce.Platform.Core.Security.Search.IUserSearchService>();

            var found = (await search.SearchUsersAsync(new UserSearchCriteria { MemberId = "member-1", Take = 1 })).Results.Single();
            found.UserName.Should().Be("rep@test.com");

            var user = await userManager.FindByIdAsync(found.Id);
            user.Roles.Should().ContainSingle(r => r.Id == "sales-rep-role");
        }
    }
}
