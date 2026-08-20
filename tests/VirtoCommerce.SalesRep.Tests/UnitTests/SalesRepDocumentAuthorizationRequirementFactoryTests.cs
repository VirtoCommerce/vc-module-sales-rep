using FluentAssertions;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Authorization;
using Xunit;
using FilePermissions = VirtoCommerce.FileExperienceApi.Core.ModuleConstants.Security.Permissions;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The IFileAuthorizationRequirementFactory bridge: its scope matches AssetEntry.Group/File.Scope of library
/// documents (so file-exp-api dispatches to it), read maps to documents:read, and every other or unknown
/// operation fails closed to documents:write.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentAuthorizationRequirementFactoryTests
{
    private readonly SalesRepDocumentAuthorizationRequirementFactory _factory = new();

    [Fact]
    public void Scope_MatchesTheDocumentsGroup()
    {
        _factory.Scope.Should().Be(ModuleConstants.DocumentsScope);
    }

    [Fact]
    public void Create_ReadOperation_RequiresDocumentsRead()
    {
        var file = new File { Scope = ModuleConstants.DocumentsScope };

        var requirement = _factory.Create(file, FilePermissions.Read);

        var documentRequirement = requirement.Should().BeOfType<SalesRepDocumentAuthorizationRequirement>().Subject;
        documentRequirement.Permission.Should().Be(ModuleConstants.Security.Permissions.DocumentsRead);
        documentRequirement.File.Should().BeSameAs(file);
    }

    [Theory]
    [InlineData(FilePermissions.Create)]
    [InlineData(FilePermissions.Update)]
    [InlineData(FilePermissions.Delete)]
    [InlineData("unknown:operation")]
    public void Create_NonReadOperation_FailsClosedToDocumentsWrite(string operation)
    {
        var requirement = _factory.Create(new File(), operation);

        requirement.Should().BeOfType<SalesRepDocumentAuthorizationRequirement>()
            .Which.Permission.Should().Be(ModuleConstants.Security.Permissions.DocumentsWrite);
    }
}
