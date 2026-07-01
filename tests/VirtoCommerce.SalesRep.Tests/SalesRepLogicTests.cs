using System;
using System.Collections.Generic;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic unit tests for the decision helpers — no collaborators, no DB, no fakes. These guard the
/// behavior that is easy to break under refactoring (name derivation, the id-level filters, and the Option A
/// sort-split that decides DB-side vs in-memory paging).
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepLogicTests
{
    // Exposes the protected static helpers (accessible from a derived type) for testing.
    private sealed class ServiceAccessor : SalesRepService
    {
        public ServiceAccessor() : base(null, null, null, null, null, null) { }

        public static string CallDeriveFullName(SalesRepDetails salesRep) => DeriveFullName(salesRep);
    }

    private sealed class SearchAccessor : SalesRepSearchService
    {
        public SearchAccessor() : base(null, null, null, null, null) { }

        public static bool CallPassesFilters(SalesRepSearchCriteria criteria, bool isLocked, int orgCount)
            => PassesFilters(criteria, isLocked, orgCount);

        public static bool CallIsMemberBackedSort(IList<SortInfo> sortInfos) => IsMemberBackedSort(sortInfos);

        public static string CallBuildMemberSort(IList<SortInfo> sortInfos) => BuildMemberSort(sortInfos);
    }

    [Fact]
    public void DeriveFullName_FromNameParts_JoinsNonEmpty()
    {
        var name = ServiceAccessor.CallDeriveFullName(new SalesRepDetails
        {
            FirstName = "Jane",
            MiddleName = null,
            LastName = "Rep",
        });

        name.Should().Be("Jane Rep");
    }

    [Fact]
    public void DeriveFullName_NoParts_FallsBackToFullName()
    {
        var name = ServiceAccessor.CallDeriveFullName(new SalesRepDetails { FullName = "Passed Full Name" });
        name.Should().Be("Passed Full Name");
    }

    [Fact]
    public void DeriveFullName_NoPartsNoFullName_FallsBackToLoginEmail()
    {
        var name = ServiceAccessor.CallDeriveFullName(new SalesRepDetails { Emails = ["rep@test.com", "extra@test.com"] });
        name.Should().Be("rep@test.com");
    }

    [Fact]
    public void PassesFilters_OnlyBlocked_ExcludesUnlocked()
    {
        var criteria = new SalesRepSearchCriteria { OnlyBlocked = true };

        SearchAccessor.CallPassesFilters(criteria, isLocked: false, orgCount: 3).Should().BeFalse();
        SearchAccessor.CallPassesFilters(criteria, isLocked: true, orgCount: 3).Should().BeTrue();
    }

    [Fact]
    public void PassesFilters_OnlyUnassigned_ExcludesRepsWithOrgs()
    {
        var criteria = new SalesRepSearchCriteria { OnlyUnassigned = true };

        SearchAccessor.CallPassesFilters(criteria, isLocked: false, orgCount: 1).Should().BeFalse();
        SearchAccessor.CallPassesFilters(criteria, isLocked: false, orgCount: 0).Should().BeTrue();
    }

    [Fact]
    public void PassesFilters_NoFilters_AlwaysPasses()
    {
        SearchAccessor.CallPassesFilters(new SalesRepSearchCriteria(), isLocked: false, orgCount: 0).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, true)]        // default → sort by name → member-backed (DB)
    [InlineData("fullname", true)]
    [InlineData("createddate", true)]
    [InlineData("modifieddate", true)]
    [InlineData("email", false)]    // account/aggregate columns → in-memory
    [InlineData("organizationscount", false)]
    [InlineData("islocked", false)]
    public void IsMemberBackedSort_ClassifiesColumn(string column, bool expected)
    {
        var sortInfos = column == null
            ? null
            : new List<SortInfo> { new() { SortColumn = column } };

        SearchAccessor.CallIsMemberBackedSort(sortInfos).Should().Be(expected);
    }

    [Theory]
    [InlineData("createddate", SortDirection.Descending, "CreatedDate:desc")]
    [InlineData("modifieddate", SortDirection.Ascending, "ModifiedDate:asc")]
    [InlineData("fullname", SortDirection.Ascending, "Name:asc")]
    [InlineData(null, SortDirection.Ascending, "Name:asc")]           // default → Name
    [InlineData("unknown", SortDirection.Descending, "Name:desc")]    // unknown → Name
    public void BuildMemberSort_MapsTokenToMemberColumn(string column, SortDirection direction, string expected)
    {
        var sortInfos = column == null
            ? null
            : new List<SortInfo> { new() { SortColumn = column, SortDirection = direction } };

        SearchAccessor.CallBuildMemberSort(sortInfos).Should().Be(expected);
    }
}
