using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for the default <see cref="SalesRepOrderStatusService"/> — each configured Order.Status value
/// becomes its own status option (1:1), and resolution maps selected statuses to the union of their underlying
/// order statuses. (Composite/override behavior is exercised end-to-end by the component tests via a stub service.)
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepOrderStatusServiceTests
{
    private static SalesRepOrderStatusService CreateService(params string[] configuredStatuses) =>
        new(new FakeLocalizableSettingService(configuredStatuses));

    [Fact]
    public async Task GetRules_MapsEachConfiguredStatus_OneToOne()
    {
        var service = CreateService("New", "Processing", "Cancelled");

        var result = await service.GetRulesAsync("B2B-store", "en-US");

        result.Select(x => x.Name).Should().Equal("New", "Processing", "Cancelled");
        result.Should().OnlyContain(x => x.OrderStatuses.Length == 1 && x.OrderStatuses[0] == x.Name);
    }

    [Fact]
    public async Task Resolve_KnownStatus_ReturnsItself()
    {
        var service = CreateService("New", "Cancelled");

        var criteria = await service.ApplyStatisticsFilterAsync("B2B-store", ["Cancelled"], new CustomerOrderStatisticsCriteria());

        criteria.Should().NotBeNull();
        criteria.Statuses.Should().Equal("Cancelled");
    }

    [Fact]
    public async Task Resolve_MultipleStatuses_ReturnsDedupedUnion()
    {
        var service = CreateService("New", "Processing", "Cancelled");

        var criteria = await service.ApplyStatisticsFilterAsync("B2B-store", ["New", "Cancelled"], new CustomerOrderStatisticsCriteria());

        criteria.Statuses.Should().BeEquivalentTo("New", "Cancelled");
    }

    [Fact]
    public async Task Resolve_ListAndStatistics_ApplySameStatuses()
    {
        var service = CreateService("New", "Processing", "Cancelled");

        var listCriteria = await service.ApplyListFilterAsync("B2B-store", ["New", "Cancelled"], new CustomerOrderSearchCriteria());
        var statsCriteria = await service.ApplyStatisticsFilterAsync("B2B-store", ["New", "Cancelled"], new CustomerOrderStatisticsCriteria());

        // The whole point of the shared resolver: both readers filter by exactly the same set.
        listCriteria.Statuses.Should().BeEquivalentTo(statsCriteria.Statuses);
    }

    [Fact]
    public async Task Resolve_Unknown_FailsClosed_And_Empty_NoFilter()
    {
        var service = CreateService("New");

        // Names given but none recognized → null (fail-closed).
        (await service.ApplyStatisticsFilterAsync("B2B-store", ["Bogus"], new CustomerOrderStatisticsCriteria())).Should().BeNull();

        // No names → criteria returned unchanged, no status filter applied.
        var noFilter = await service.ApplyStatisticsFilterAsync("B2B-store", null, new CustomerOrderStatisticsCriteria());
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
}
