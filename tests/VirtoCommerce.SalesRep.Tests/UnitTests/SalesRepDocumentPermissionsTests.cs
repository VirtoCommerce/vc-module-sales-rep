using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.Core;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The documents-library authorization matrix (anonymous / no-permission / read / write / admin × read and
/// write operations): fail-closed everywhere, write implies read, Administrator always passes, anonymous
/// never does — over the SalesRepDocumentPermissions predicate the REST controller reads call. (The GraphQL
/// queries and file-experience-api surfaces run SalesRepDocumentAuthorizationHandler, which has its own
/// equivalent matrix test.)
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentPermissionsTests
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
    public void Access_PermissionMatrix(bool authenticated, string[] permissions, bool administrator, string requiredPermission, bool expected)
    {
        var user = CreateUser(authenticated, permissions, administrator);

        HasAccess(user, requiredPermission).Should().Be(expected);
    }

    [Fact]
    public void Access_LimitedPermissionsClaim_RestrictsEffectivePermissions()
    {
        // Global write claim present, but limited_permissions narrows the token to read only.
        var claims = new List<Claim>
        {
            new(PlatformConstants.Security.Claims.PermissionClaimType, DocumentsWrite),
            new(PlatformConstants.Security.Claims.LimitedPermissionsClaimType, DocumentsRead),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        user.HasReadAccess().Should().BeTrue();
        user.HasWriteAccess().Should().BeFalse();
    }

    private static bool HasAccess(ClaimsPrincipal user, string requiredPermission)
    {
        return requiredPermission == DocumentsWrite
            ? user.HasWriteAccess()
            : user.HasReadAccess();
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
}
