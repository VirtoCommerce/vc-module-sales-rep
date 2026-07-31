using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for the default <see cref="SalesRepOrderFilterRuleResolver"/> — each status the store's orders
/// actually use becomes its own status option (1:1), labeled from the configured Order.Status dictionary, and
/// resolution maps selected statuses to the union of their underlying order statuses. (Composite/override behavior is
/// exercised end-to-end by the component tests via a stub service.)
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepOrderFilterRuleResolverTests
{
    private const string StoreId = "B2B-store";

    /// <summary>Statuses in the configured Order.Status dictionary; <paramref name="usedStatuses"/> defaults to
    /// "every configured status is in use" so a test only spells out the order side when that is the point.</summary>
    private static SalesRepOrderFilterRuleResolver CreateService(string[] configuredStatuses, string[] usedStatuses = null) =>
        new(new FakeLocalizableSettingService(configuredStatuses), new FakeOrderStatusService(usedStatuses ?? configuredStatuses));

    /// <summary>The caller scope a discovery query hands the resolver: one served organization, orders created by the rep.</summary>
    private static SalesRepFilterRuleContext Context(params string[] organizationIds) =>
        SalesRepFilterRuleContext.Create(StoreId, "en-US", organizationIds.Length > 0 ? organizationIds : ["org-1"], "rep-1");

    /// <summary>Order statistics criteria carrying the same scope the readers apply.</summary>
    private static CustomerOrderStatisticsCriteria StatisticsCriteria() =>
        new() { OrganizationIds = ["org-1"], CustomerId = "rep-1", StoreId = StoreId };

    [Fact]
    public async Task GetRules_MapsEachUsedStatus_OneToOne()
    {
        var service = CreateService(["New", "Processing", "Cancelled"]);

        var result = await service.GetRulesAsync(Context());

        result.Select(x => x.Name).Should().Equal("New", "Processing", "Cancelled");
        result.Should().OnlyContain(x => x.OrderStatuses.Count == 1 && x.OrderStatuses[0] == x.Name);
    }

    [Fact]
    public async Task GetRules_OmitsConfiguredStatusesNoOrderUses()
    {
        var service = CreateService(["New", "Processing", "Cancelled"], ["New", "Cancelled"]);

        var result = await service.GetRulesAsync(Context());

        // "Processing" is configured but no order has it — offering it would only ever return an empty list.
        result.Select(x => x.Name).Should().Equal("New", "Cancelled");
    }

    [Fact]
    public async Task GetRules_AppendsUsedStatusesMissingFromTheDictionary()
    {
        var service = CreateService(["New", "Processing"], ["New", "Shipped", "Processing", "AwaitingErp"]);

        var result = await service.GetRulesAsync(Context());

        // Configured statuses keep the dictionary's curated order; statuses that arrived with the orders (e.g. from an
        // ERP sync) follow, alphabetically, labeled with the raw status.
        result.Select(x => x.Name).Should().Equal("New", "Processing", "AwaitingErp", "Shipped");
        result.Single(x => x.Name == "Shipped").LocalizedName.Should().Be("Shipped");
    }

    [Fact]
    public async Task GetRules_NoOrders_ReturnsNoRules()
    {
        var service = CreateService(["New", "Processing"], []);

        var result = await service.GetRulesAsync(Context());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRules_ReadsStatusesWithinTheCallerScope()
    {
        // The whole point of the scoped vocabulary: statuses come from the orders the caller's list will search
        // (served organizations + the rep as creator), never store-wide — otherwise a rule is offered that the list
        // returns nothing for.
        var statusService = new FakeOrderStatusService(["New"]);
        var service = new SalesRepOrderFilterRuleResolver(new FakeLocalizableSettingService(["New"]), statusService);

        await service.GetRulesAsync(SalesRepFilterRuleContext.Create(StoreId, "en-US", ["org-1", "org-2"], "rep-1"));

        statusService.LastCriteria.OrganizationIds.Should().Equal("org-1", "org-2");
        statusService.LastCriteria.CustomerId.Should().Be("rep-1");
        statusService.LastCriteria.StoreId.Should().Be(StoreId);
    }

    [Fact]
    public async Task Resolve_ReadsStatusesWithinTheReaderScope()
    {
        // On the apply path the scope comes from the reader's own criteria, so what resolves is exactly what the
        // discovery query offered for that same list.
        var statusService = new FakeOrderStatusService(["New"]);
        var service = new SalesRepOrderFilterRuleResolver(new FakeLocalizableSettingService(["New"]), statusService);
        var criteria = new CustomerOrderSearchCriteria { OrganizationIds = ["org-7"], CustomerId = "rep-9" };

        await service.ApplyListFilterAsync(StoreId, "New", criteria);

        statusService.LastCriteria.OrganizationIds.Should().Equal("org-7");
        statusService.LastCriteria.CustomerId.Should().Be("rep-9");
    }

    [Fact]
    public async Task Resolve_StatusMissingFromTheDictionary_Applies()
    {
        var service = CreateService(["New"], ["New", "AwaitingErp"]);

        var criteria = await service.ApplyStatisticsFilterAsync(StoreId, "AwaitingErp", StatisticsCriteria());

        criteria.Should().NotBeNull();
        criteria.Statuses.Should().Equal("AwaitingErp");
    }

    [Fact]
    public async Task Resolve_ConfiguredButUnusedStatus_FailsClosed()
    {
        var service = CreateService(["New", "Processing"], ["New"]);

        // It is not an offered rule, so it resolves like any unknown name (fail-closed) rather than silently
        // returning every order.
        (await service.ApplyStatisticsFilterAsync(StoreId, "Processing", StatisticsCriteria())).Should().BeNull();
    }

    [Fact]
    public async Task Resolve_KnownRule_MapsToItsStatus()
    {
        var service = CreateService(["New", "Cancelled"]);

        var criteria = await service.ApplyStatisticsFilterAsync(StoreId, "Cancelled", StatisticsCriteria());

        criteria.Should().NotBeNull();
        criteria.Statuses.Should().Equal("Cancelled");
    }

    [Fact]
    public async Task Resolve_IsCaseInsensitive()
    {
        var service = CreateService(["New", "Cancelled"]);

        var criteria = await service.ApplyStatisticsFilterAsync(StoreId, "cancelled", StatisticsCriteria());

        criteria.Statuses.Should().Equal("Cancelled");
    }

    [Fact]
    public async Task Resolve_ListAndStatistics_ApplySameStatuses()
    {
        var service = CreateService(["New", "Processing", "Cancelled"]);

        var listCriteria = await service.ApplyListFilterAsync(StoreId, "Cancelled", new CustomerOrderSearchCriteria { OrganizationIds = ["org-1"], CustomerId = "rep-1" });
        var statsCriteria = await service.ApplyStatisticsFilterAsync(StoreId, "Cancelled", StatisticsCriteria());

        // The whole point of the shared resolver: both readers filter by exactly the same rule → same statuses.
        listCriteria.Statuses.Should().BeEquivalentTo(statsCriteria.Statuses);
    }

    [Fact]
    public async Task Resolve_Unknown_FailsClosed_And_Empty_NoFilter()
    {
        var service = CreateService(["New"]);

        // A rule name was given but is unrecognized → null (fail-closed).
        (await service.ApplyStatisticsFilterAsync(StoreId, "Bogus", StatisticsCriteria())).Should().BeNull();

        // No filter → criteria returned unchanged, no status filter applied (the baseline set).
        var noFilter = await service.ApplyStatisticsFilterAsync(StoreId, null, StatisticsCriteria());
        noFilter.Should().NotBeNull();
        noFilter.Statuses.Should().BeNull();
    }

    /// <summary>Returns the given statuses as the Order.Status dictionary (Key = raw status, Value = label).</summary>
    private sealed class FakeLocalizableSettingService : ILocalizableSettingService
    {
        private readonly string[] _statuses;

        public FakeLocalizableSettingService(string[] statuses) => _statuses = statuses;

        public Task<IList<KeyValue>> GetValuesAsync(string settingName, string languageCode)
            => Task.FromResult<IList<KeyValue>>(_statuses.Select(s => new KeyValue { Key = s, Value = s }).ToList());

        public Task<LocalizableSettingsAndLanguages> GetSettingsAndLanguagesAsync() => throw new System.NotSupportedException();
        public Task<string> TranslateAsync(string key, string settingName, string languageCode) => throw new System.NotSupportedException();
        public Task SaveAsync(string settingName, IList<DictionaryItem> items) => throw new System.NotSupportedException();
        public Task DeleteAsync(string settingName, IList<string> values) => throw new System.NotSupportedException();
    }

    /// <summary>Stands in for the DISTINCT over the store's orders: the statuses the store's orders actually use.</summary>
    private sealed class FakeOrderStatusService : ISalesRepOrderStatusService
    {
        private readonly string[] _statuses;

        public FakeOrderStatusService(string[] statuses) => _statuses = statuses;

        /// <summary>The scope it was last asked for — the resolver must pass the caller's scope through, not a store-wide one.</summary>
        public SalesRepScopeCriteria LastCriteria { get; private set; }

        public Task<IList<string>> GetUsedStatusesAsync(SalesRepScopeCriteria criteria)
        {
            LastCriteria = criteria;
            return Task.FromResult<IList<string>>(_statuses.ToList());
        }
    }
}
