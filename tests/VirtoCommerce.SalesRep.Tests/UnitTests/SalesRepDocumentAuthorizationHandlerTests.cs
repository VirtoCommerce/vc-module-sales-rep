using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.Data.Authorization;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The documents-library authorization matrix (anonymous / no-permission / read / write / admin × read and
/// write operations): fail-closed everywhere, write implies read, Administrator always passes — and, unlike
/// the default file-exp-api handler this one replaces, no ownerless-file shortcut for anonymous callers.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentAuthorizationHandlerTests
{
    private const string DocumentsRead = ModuleConstants.Security.Permissions.DocumentsRead;
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;

    [Theory]
    // anonymous: everything denied
    [InlineData(false, new string[0], false, DocumentsRead, false)]
    [InlineData(false, new string[0], false, DocumentsWrite, false)]
    // authenticated without permissions: everything denied
    [InlineData(true, new string[0], false, DocumentsRead, false)]
    [InlineData(true, new string[0], false, DocumentsWrite, false)]
    // read permission: read allowed, write denied
    [InlineData(true, new[] { DocumentsRead }, false, DocumentsRead, true)]
    [InlineData(true, new[] { DocumentsRead }, false, DocumentsWrite, false)]
    // write permission implies read
    [InlineData(true, new[] { DocumentsWrite }, false, DocumentsRead, true)]
    [InlineData(true, new[] { DocumentsWrite }, false, DocumentsWrite, true)]
    // unrelated permission: denied
    [InlineData(true, new[] { "sales-rep:access" }, false, DocumentsRead, false)]
    // administrator passes everything without permission claims
    [InlineData(true, new string[0], true, DocumentsRead, true)]
    [InlineData(true, new string[0], true, DocumentsWrite, true)]
    public async Task Handle_PermissionMatrix(bool authenticated, string[] permissions, bool administrator, string requiredPermission, bool expected)
    {
        var user = CreateUser(authenticated, permissions, administrator);

        var authorized = await AuthorizeAsync(user, requiredPermission);

        authorized.Should().Be(expected);
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

    [Fact]
    public async Task Handle_LimitedPermissionsClaim_RestrictsEffectivePermissions()
    {
        // Global write claim present, but limited_permissions narrows the token to read only.
        var claims = new List<Claim>
        {
            new(PlatformConstants.Security.Claims.PermissionClaimType, DocumentsWrite),
            new(PlatformConstants.Security.Claims.LimitedPermissionsClaimType, DocumentsRead),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        (await AuthorizeAsync(user, DocumentsRead)).Should().BeTrue();
        (await AuthorizeAsync(user, DocumentsWrite)).Should().BeFalse();
    }

    private static ClaimsPrincipal CreateUser(bool authenticated, string[] permissions, bool administrator)
    {
        var claims = permissions
            .Select(permission => new Claim(PlatformConstants.Security.Claims.PermissionClaimType, permission))
            .ToList();

        if (administrator)
        {
            claims.Add(new Claim(ClaimTypes.Role, PlatformConstants.Security.SystemRoles.Administrator));
        }

        var identity = authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity();

        return new ClaimsPrincipal(identity);
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
