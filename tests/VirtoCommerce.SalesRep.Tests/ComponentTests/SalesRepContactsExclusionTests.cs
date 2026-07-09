using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.ProfileExperienceApiModule.Data.Commands;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Verifies the override of ProfileExperienceApi's contacts search: an organization's contact roster
/// (storefront <c>organization.contacts</c> → <see cref="SearchContactsQuery"/>) returns non-rep contacts only.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepContactsExclusionTests
{
    [Fact]
    public async Task OrganizationContacts_OrgScoped_ExcludesSalesReps()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");

        // A regular contact belonging to org-1 — must remain in the roster.
        var regular = await ctx.SeedContactAsync("regular-1", c =>
        {
            c.Name = "Regular Person";
            c.Organizations = ["org-1"];
        });

        // A Sales Rep serving org-1 — its contact is also a member of org-1, so it would appear in the roster.
        var rep = await ctx.CreateRepAsync("Reppy", "McRep", "reppy@test.com", "org-1");

        await ctx.IndexMembersAsync("org-1", regular.Id, rep.Id);

        // Baseline: a plain member search of the org roster returns BOTH the regular contact and the rep.
        // This proves the rep IS a roster member, so its absence below is a real exclusion (not "never there").
        var memberSearch = ctx.GetRequiredService<IMemberSearchService>();
        var rosterCriteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        rosterCriteria.MemberType = nameof(Contact);
        rosterCriteria.MemberId = "org-1";
        rosterCriteria.Take = 100;
        var baseline = await memberSearch.SearchMembersAsync(rosterCriteria);
        baseline.Results.Select(m => m.Id).Should().Contain(regular.Id).And.Contain(rep.Id);

        // Act: the overridden SearchContactsQuery handler (what organization.contacts resolves through).
        var handler = ctx.GetRequiredService<IRequestHandler<SearchContactsQuery, MemberSearchResult>>();
        var result = await handler.Handle(
            new SearchContactsQuery { MemberId = "org-1", Take = 100 },
            CancellationToken.None);

        // Assert: the non-rep contact remains; the rep is excluded.
        var ids = result.Results.Select(m => m.Id).ToList();
        ids.Should().Contain(regular.Id);
        ids.Should().NotContain(rep.Id);
    }
}
