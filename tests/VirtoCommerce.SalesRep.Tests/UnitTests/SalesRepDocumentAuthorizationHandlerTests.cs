using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Authorization;
using Xunit;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The single owner of the documents authorization matrix (the REST endpoints use plain single-permission
/// [Authorize] attributes handled by the platform): read means read, write means write (roles compose them),
/// Administrator passes unless a limited token confines it, anonymous never passes. Plus the library-document
/// claim rule — a scope file is readable only once its owner stamp is set, so an uploaded-but-unregistered blob
/// is served to no one (unlike the default file-exp-api handler's ownerless-file shortcut), while
/// documents:write holders can still delete such blobs for cleanup.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentAuthorizationHandlerTests
{
    private const string DocumentsRead = ModuleConstants.Security.Permissions.DocumentsRead;
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;

    [Fact]
    public async Task Handle_ReadMeansReadAndWriteMeansWrite()
    {
        // Neither permission implies the other — roles compose them (the seeded manager role carries both).
        var reader = CreateUser(DocumentsRead);
        var writer = CreateUser(DocumentsWrite);

        (await AuthorizeAsync(reader, DocumentsRead)).Should().BeTrue();
        (await AuthorizeAsync(reader, DocumentsWrite)).Should().BeFalse();
        (await AuthorizeAsync(writer, DocumentsWrite)).Should().BeTrue();
        (await AuthorizeAsync(writer, DocumentsRead)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnknownPermissionRequirement_IsDenied()
    {
        // The factory normalizes every non-read file permission to documents:write before a requirement is
        // created, so an unknown permission means a foreign/malformed requirement — denied outright, even for
        // holders of both document permissions.
        (await AuthorizeAsync(CreateUser(DocumentsWrite), "unknown:permission")).Should().BeFalse();
        (await AuthorizeAsync(CreateUser(DocumentsRead), "unknown:permission")).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Administrator_PassesBothBranches()
    {
        var administrator = CreateAdministrator();

        (await AuthorizeAsync(administrator, DocumentsRead)).Should().BeTrue();
        (await AuthorizeAsync(administrator, DocumentsWrite)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LimitedToken_ConfinesEvenAnAdministrator()
    {
        // Platform semantics: a limited_permissions token grants ONLY its listed permissions.
        var limitedAdministrator = CreateAdministrator(limitedPermissions: DocumentsRead);

        (await AuthorizeAsync(limitedAdministrator, DocumentsRead)).Should().BeTrue();
        (await AuthorizeAsync(limitedAdministrator, DocumentsWrite)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AuthenticatedWithoutPermissions_IsDenied()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims: [], authenticationType: "Test"));

        (await AuthorizeAsync(user, DocumentsRead)).Should().BeFalse();
        (await AuthorizeAsync(user, DocumentsWrite)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithoutThePermission_FileStateNeverGrants()
    {
        // The file gate can only veto — a passing file state (claimed for reads, unclaimed for writes) must
        // never substitute for the missing permission.
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims: [], authenticationType: "Test"));

        (await AuthorizeAsync(user, DocumentsRead, ClaimedFile())).Should().BeFalse();
        (await AuthorizeAsync(user, DocumentsWrite, UnclaimedFile())).Should().BeFalse();
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

    // Defence-in-depth: only the LIBRARY's complete claim (owner id + owner type) opens the read gate — a scope
    // file carrying some other module's owner stamp, or a half-stamp missing the id, is neither readable nor
    // deletable through the generic surfaces.
    [Fact]
    public async Task Handle_ForeignOwnedFile_IsNeitherReadableNorWritable()
    {
        var file = UnclaimedFile();
        file.OwnerEntityId = "foreign-1";
        file.OwnerEntityType = "SomeOther.Module.Entity";

        (await AuthorizeAsync(CreateUser(DocumentsRead), DocumentsRead, file)).Should().BeFalse();
        (await AuthorizeAsync(CreateUser(DocumentsWrite), DocumentsWrite, file)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OwnerTypeWithoutOwnerId_IsNeitherReadableNorWritable()
    {
        var file = UnclaimedFile();
        file.OwnerEntityType = typeof(SalesRepDocumentMetadata).FullName;

        (await AuthorizeAsync(CreateUser(DocumentsRead), DocumentsRead, file)).Should().BeFalse();
        (await AuthorizeAsync(CreateUser(DocumentsWrite), DocumentsWrite, file)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DeleteOfUnclaimedFile_IsAllowedForWriteHolders()
    {
        // The cleanup path for abandoned uploads.
        var writer = CreateUser(DocumentsWrite);

        (await AuthorizeAsync(writer, DocumentsWrite, UnclaimedFile())).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DeleteOfClaimedFile_IsDeniedForWriteHolders()
    {
        // A claimed document is managed only through the module's own endpoints, which cascade the metadata row.
        var writer = CreateUser(DocumentsWrite);

        (await AuthorizeAsync(writer, DocumentsWrite, ClaimedFile())).Should().BeFalse();
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

    private static File ClaimedFile()
    {
        var file = new File { Id = "file-1", Scope = ModuleConstants.DocumentsScope };
        file.SetOwner<SalesRepDocumentMetadata>("document-1");
        return file;
    }

    private static ClaimsPrincipal CreateUser(string permission)
    {
        var claims = new[] { new Claim(PlatformConstants.Security.Claims.PermissionClaimType, permission) };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ClaimsPrincipal CreateAdministrator(string limitedPermissions = null)
    {
        var identity = new ClaimsIdentity(claims: [], authenticationType: "Test");
        identity.AddClaim(new Claim(identity.RoleClaimType, PlatformConstants.Security.SystemRoles.Administrator));
        if (limitedPermissions != null)
        {
            identity.AddClaim(new Claim(PlatformConstants.Security.Claims.LimitedPermissionsClaimType, limitedPermissions));
        }

        return new ClaimsPrincipal(identity);
    }

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string permission, File file = null)
    {
        var handler = new SalesRepDocumentAuthorizationHandler();
        var requirement = new SalesRepDocumentAuthorizationRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, resource: file);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }
}
