using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

// The scope every analytics read carries: the input it cannot make safe is refused there.
public class SalesRepAnalyticsScopeTests
{
    private readonly SalesRepAnalyticsScope _scope = new();

    [Fact]
    public void CreateCriteria_CarriesBothScopeFilters()
    {
        var criteria = _scope.CreateCriteria(
            "B2B-store", ["org-1", "org-2"], [AnalyticsConstants.EventNames.Search], [], null, null);

        var filters = criteria.DimensionFilters;

        filters.Should().Contain(x => x.DimensionName == AnalyticsConstants.UserDimensions.SessionKind);
        filters.Should().Contain(x =>
            x.DimensionName == AnalyticsConstants.UserDimensions.OrganizationId &&
            x.Values.Count == 2);
    }

    // The analytics module DROPS a valueless filter, so an empty list reads every organization in the property.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateCriteria_WithoutOrganizations_Throws(bool nullList)
    {
        IList<string> organizationIds = nullList ? null : [];

        var act = () => _scope.CreateCriteria(
            "B2B-store", organizationIds, [AnalyticsConstants.EventNames.Search], [], null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*at least one organization*");
    }

    [Fact]
    public void CreateScopeFilters_CopiesTheOrganizationList()
    {
        var organizationIds = new List<string> { "org-1" };

        var filter = _scope.CreateScopeFilters(organizationIds)
            .Single(x => x.DimensionName == AnalyticsConstants.UserDimensions.OrganizationId);
        organizationIds.Add("org-2");

        filter.Values.Should().ContainSingle("the filter must not follow a list the caller keeps mutating");
    }
}
