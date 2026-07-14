using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for the default <see cref="SalesRepOrderStatusService"/> — each configured Order.Status value
/// becomes its own status option (1:1), resolution maps a selected status back to its underlying order statuses,
/// and an order's raw status is localized from the dictionary. (Composite/override behavior is exercised end-to-end
/// by the component tests via a stub status service.)
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepOrderStatusServiceTests
{
    private static SalesRepOrderStatusService CreateService(params string[] configuredStatuses) =>
        new(new FakeLocalizableSettingService(configuredStatuses));

    [Fact]
    public async Task GetStatuses_MapsEachConfiguredStatus_OneToOne()
    {
        var service = CreateService("New", "Processing", "Cancelled");

        var result = await service.GetStatusesAsync("B2B-store", "en-US");

        result.Select(x => x.Name).Should().Equal("New", "Processing", "Cancelled");
        result.Should().OnlyContain(x => x.OrderStatuses.Length == 1 && x.OrderStatuses[0] == x.Name);
    }

    [Fact]
    public async Task Resolve_KnownStatus_ReturnsItself()
    {
        var service = CreateService("New", "Cancelled");

        (await service.ResolveOrderStatusesAsync("B2B-store", "Cancelled")).Should().Equal("Cancelled");
    }

    [Fact]
    public async Task Resolve_UnknownOrEmpty_ReturnsEmpty()
    {
        var service = CreateService("New");

        (await service.ResolveOrderStatusesAsync("B2B-store", "Bogus")).Should().BeEmpty();
        (await service.ResolveOrderStatusesAsync("B2B-store", null)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetLocalizedStatuses_LocalizesEachConfiguredStatus()
    {
        var service = CreateService("New", "Cancelled");

        var map = await service.GetLocalizedStatusesAsync("B2B-store", "en-US");

        // Raw status → localized label, straight from the dictionary (fake renders the label "loc:<raw>").
        map["Cancelled"].Should().Be("loc:Cancelled");
        map["New"].Should().Be("loc:New");
    }

    /// <summary>Returns the given statuses as the Order.Status dictionary (Key = raw status, Value = label).</summary>
    private sealed class FakeLocalizableSettingService : ILocalizableSettingService
    {
        private readonly string[] _statuses;

        public FakeLocalizableSettingService(string[] statuses) => _statuses = statuses;

        public Task<IList<KeyValue>> GetValuesAsync(string settingName, string languageCode)
            => Task.FromResult<IList<KeyValue>>(_statuses.Select(s => new KeyValue { Key = s, Value = "loc:" + s }).ToList());

        public Task<LocalizableSettingsAndLanguages> GetSettingsAndLanguagesAsync() => throw new System.NotSupportedException();
        public Task<string> TranslateAsync(string key, string settingName, string languageCode) => throw new System.NotSupportedException();
        public Task SaveAsync(string settingName, IList<DictionaryItem> items) => throw new System.NotSupportedException();
        public Task DeleteAsync(string settingName, IList<string> values) => throw new System.NotSupportedException();
    }
}
