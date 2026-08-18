using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.ExperienceApi.Authorization;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The handler is a thin delegate to the SalesRepDocumentPermissions predicates (whose full authorization
/// matrix has its own test); pinned here is what the handler itself owns: the requirement→predicate branch
/// and the fail-closed denial — unlike the default file-exp-api handler this one replaces, there is no
/// ownerless-file shortcut for anonymous callers.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentAuthorizationHandlerTests
{
    private const string DocumentsRead = ModuleConstants.Security.Permissions.DocumentsRead;
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;

    [Fact]
    public async Task Handle_ReadRequirement_RunsTheReadPredicate()
    {
        // A read-permission holder passes the read requirement but not the write one.
        var reader = CreateUser(DocumentsRead);

        (await AuthorizeAsync(reader, DocumentsRead)).Should().BeTrue();
        (await AuthorizeAsync(reader, DocumentsWrite)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonReadRequirement_RunsTheWritePredicate()
    {
        // Any non-read permission fails closed to the write predicate; a write holder passes both branches.
        var writer = CreateUser(DocumentsWrite);

        (await AuthorizeAsync(writer, "unknown:permission")).Should().BeTrue();
        (await AuthorizeAsync(writer, DocumentsRead)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AnonymousWithoutFileOwner_IsDenied()
    {
        // The default FileAuthorizationHandler grants any ownerless file, even anonymously — the exact hole
        // this handler closes for the documents scope.
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var authorized = await AuthorizeAsync(anonymous, DocumentsRead);

        authorized.Should().BeFalse();
    }

    private static ClaimsPrincipal CreateUser(string permission)
    {
        var claims = new[] { new Claim(PlatformConstants.Security.Claims.PermissionClaimType, permission) };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string permission)
    {
        var handler = new SalesRepDocumentAuthorizationHandler();
        var requirement = new SalesRepDocumentAuthorizationRequirement(file: null, permission);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }
}
