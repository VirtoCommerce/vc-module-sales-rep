using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The analytics-diagnostics REST endpoint invoked directly. Diagnostics names the store's analytics property,
/// its credential kind and the Google errors behind a failure, so the permission it declares is what keeps that
/// out of a plain rep's hands: it is asserted here rather than assumed.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepAnalyticsDiagnosticsControllerActionsTests
{
    [Fact]
    public void RunAnalyticsDiagnostics_DeclaresTheDiagnosticsPermission()
    {
        var attribute = typeof(SalesRepAnalyticsDiagnosticsController)
            .GetMethod(nameof(SalesRepAnalyticsDiagnosticsController.RunAnalyticsDiagnostics))
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single();

        attribute.Policy.Should().Be(ModuleConstants.Security.Permissions.Diagnostics);
    }

    // sales-rep:access alone must not reach it: the rep-facing screens carry no diagnostics of their own.
    [Fact]
    public void TheDiagnosticsPermissionIsNotTheOneEveryRepCarries()
    {
        ModuleConstants.Security.Permissions.Diagnostics
            .Should().NotBe(ModuleConstants.Security.Permissions.Access);
    }

    [Fact]
    public async Task RunAnalyticsDiagnostics_PassesTheStoreAndLiveDataFlagThrough_AndReturnsTheReport()
    {
        var report = new AnalyticsDiagnosticsResult
        {
            Checks = [new AnalyticsDiagnosticsCheck { Stage = "configuration", Status = "Passed" }],
        };
        var service = new FakeDiagnosticsService(report);
        var controller = new SalesRepAnalyticsDiagnosticsController(service);

        var response = await controller.RunAnalyticsDiagnostics("B2B-store", includeLiveData: false);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(report);
        service.StoreId.Should().Be("B2B-store");
        service.IncludeLiveData.Should().BeFalse();
    }

    private sealed class FakeDiagnosticsService : ISalesRepAnalyticsDiagnosticsService
    {
        private readonly AnalyticsDiagnosticsResult _report;

        public FakeDiagnosticsService(AnalyticsDiagnosticsResult report)
        {
            _report = report;
        }

        public string StoreId { get; private set; }

        public bool? IncludeLiveData { get; private set; }

        public Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, bool includeLiveData)
        {
            StoreId = storeId;
            IncludeLiveData = includeLiveData;

            return Task.FromResult(_report);
        }
    }
}
