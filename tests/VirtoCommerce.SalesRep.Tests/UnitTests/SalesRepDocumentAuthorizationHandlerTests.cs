using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Authorization;
using Xunit;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The handler delegates the permission decision to the SalesRepDocumentPermissions predicates (whose full
/// authorization matrix has its own test); pinned here is what the handler itself owns: the requirement→predicate
/// branch, the fail-closed denial, and the library-document claim rule — a scope file is readable only once its
/// owner stamp is set, so an uploaded-but-unregistered blob is served to no one (unlike the default file-exp-api
/// handler's ownerless-file shortcut), while documents:write holders can still delete such blobs for cleanup.
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
    public async Task Handle_ReadOfClaimedFile_IsAllowed()
    {
        var reader = CreateUser(DocumentsRead);

        (await AuthorizeAsync(reader, DocumentsRead, ClaimedFile())).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReadOfUnclaimedFile_IsDeniedRegardlessOfPermissions()
    {
        // An uploaded-but-unregistered blob is not a library document: unreadable even for read/write holders.
        var file = UnclaimedFile();

        (await AuthorizeAsync(CreateUser(DocumentsRead), DocumentsRead, file)).Should().BeFalse();
        (await AuthorizeAsync(CreateUser(DocumentsWrite), DocumentsRead, file)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DeleteOfUnclaimedFile_IsAllowedForWriteHolders()
    {
        // The cleanup path for abandoned uploads: write/delete stays permission-only.
        var writer = CreateUser(DocumentsWrite);

        (await AuthorizeAsync(writer, DocumentsWrite, UnclaimedFile())).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AnonymousWithoutFileOwner_IsDenied()
    {
        // The default FileAuthorizationHandler grants any ownerless file, even anonymously — the exact hole
        // this handler closes for the documents scope.
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var authorized = await AuthorizeAsync(anonymous, DocumentsRead, UnclaimedFile());

        authorized.Should().BeFalse();
    }

    private static File UnclaimedFile() => new() { Id = "file-1", Scope = ModuleConstants.DocumentsScope };

    private static File ClaimedFile() => new()
    {
        Id = "file-1",
        Scope = ModuleConstants.DocumentsScope,
        OwnerEntityId = "document-1",
        OwnerEntityType = nameof(SalesRepDocumentMetadata),
    };

    private static ClaimsPrincipal CreateUser(string permission)
    {
        var claims = new[] { new Claim(PlatformConstants.Security.Claims.PermissionClaimType, permission) };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string permission, File file = null)
    {
        var handler = new SalesRepDocumentAuthorizationHandler();
        var requirement = new SalesRepDocumentAuthorizationRequirement(file, permission);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }
}
