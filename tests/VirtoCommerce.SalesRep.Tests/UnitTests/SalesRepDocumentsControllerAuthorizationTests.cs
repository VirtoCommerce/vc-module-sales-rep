using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// Attribute-presence guard for the documents REST controller. The direct-invocation component harness cannot run
/// the declarative <c>[Authorize(documents:write)]</c> policy (no TestServer), so this reflection test pins the
/// declarative surface: the write actions carry the write-permission policy, the two storefront reads are
/// [AllowAnonymous], the reads never carry the write policy, and the controller itself is [Authorize] by default.
/// It fails the moment any of those attributes is silently removed or its policy string drifts.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentsControllerAuthorizationTests
{
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;
    private static readonly Type ControllerType = typeof(SalesRepDocumentsController);

    [Theory]
    [InlineData(nameof(SalesRepDocumentsController.Upload))]
    [InlineData(nameof(SalesRepDocumentsController.UpdateMetadata))]
    [InlineData(nameof(SalesRepDocumentsController.Pin))]
    [InlineData(nameof(SalesRepDocumentsController.Unpin))]
    [InlineData(nameof(SalesRepDocumentsController.Delete))]
    public void WriteAction_RequiresTheDocumentsWritePolicy(string actionName)
    {
        var authorize = GetAction(actionName).GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();

        authorize.Should().ContainSingle("the write action must carry exactly one [Authorize]")
            .Which.Policy.Should().Be(DocumentsWrite);
    }

    [Theory]
    [InlineData(nameof(SalesRepDocumentsController.Download))]
    [InlineData(nameof(SalesRepDocumentsController.GetInfo))]
    public void StorefrontRead_IsAllowAnonymous(string actionName)
    {
        GetAction(actionName).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
            .Should().NotBeEmpty("the storefront read replaces the default policy with an in-action check");
    }

    [Theory]
    [InlineData(nameof(SalesRepDocumentsController.Search))]
    [InlineData(nameof(SalesRepDocumentsController.GetCategories))]
    [InlineData(nameof(SalesRepDocumentsController.Download))]
    [InlineData(nameof(SalesRepDocumentsController.GetInfo))]
    public void ReadAction_DoesNotCarryTheWritePolicy(string actionName)
    {
        GetAction(actionName).GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().NotContain(x => x.Policy == DocumentsWrite,
                "reads accept read OR write OR Administrator and must not be gated on the write permission");
    }

    [Fact]
    public void Controller_IsAuthorizeByDefault()
    {
        ControllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().ContainSingle("the controller class is [Authorize] (authenticated) by default")
            .Which.Policy.Should().BeNull("the class-level [Authorize] carries no specific policy");
    }

    private static MethodInfo GetAction(string name)
        => ControllerType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Action '{name}' was not found on {ControllerType.Name}.");
}
