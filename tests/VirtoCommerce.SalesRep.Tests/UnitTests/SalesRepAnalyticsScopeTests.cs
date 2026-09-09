using System;
using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The single construction site for scoped analytics reads. Going through it is what is supposed to make it
/// impossible to forget the scope filters, so the one input it cannot make safe has to be refused here.
/// </summary>
public class SalesRepAnalyticsScopeTests
{
    [Fact]
    public void CreateCriteria_CarriesBothScopeFilters()
    {
        var criteria = SalesRepAnalyticsScope.CreateCriteria(
            "B2B-store", ["org-1", "org-2"], [AnalyticsConstants.EventNames.Search], [], null, null);

        var filters = criteria.DimensionFilters;

        filters.Should().Contain(x => x.DimensionName == AnalyticsConstants.UserDimensions.SessionKind);
        filters.Should().Contain(x =>
            x.DimensionName == AnalyticsConstants.UserDimensions.OrganizationId &&
            x.Values.Count == 2);
    }

    // The analytics module DROPS a filter that carries no values, so an empty list would come back scoped by
    // session kind alone — every organization in the property. Callers guard upstream; this refuses it outright
    // so the guarantee lives where it is claimed.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateCriteria_WithoutOrganizations_Throws(bool nullList)
    {
        IList<string> organizationIds = nullList ? null : [];

        var act = () => SalesRepAnalyticsScope.CreateCriteria(
            "B2B-store", organizationIds, [AnalyticsConstants.EventNames.Search], [], null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*at least one organization*");
    }

    [Fact]
    public void CreateOrganizationFilter_CopiesTheList()
    {
        var organizationIds = new List<string> { "org-1" };

        var filter = SalesRepAnalyticsScope.CreateOrganizationFilter(organizationIds);
        organizationIds.Add("org-2");

        filter.Values.Should().ContainSingle("the filter must not follow a list the caller keeps mutating");
    }
}
