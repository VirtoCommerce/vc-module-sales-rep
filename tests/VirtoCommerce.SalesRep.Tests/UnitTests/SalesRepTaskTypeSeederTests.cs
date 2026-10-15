using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;
using TaskManagementSettings = VirtoCommerce.TaskManagement.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// "Call" has to be a real TaskManagement.TaskTypes value, so admins can pick it and filter by it in the Tasks app.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepTaskTypeSeederTests
{
    [Fact]
    public void AddCallType_AppendsToTheBuiltInList_Once()
    {
        var taskTypes = new SettingDescriptor { Name = TaskManagementSettings.TaskTypes.Name, AllowedValues = ["Other", "Customer Support"] };

        SalesRepTaskTypeSeeder.AddCallType(taskTypes);
        SalesRepTaskTypeSeeder.AddCallType(taskTypes);

        taskTypes.AllowedValues.Should().Equal("Other", "Customer Support", "Call");
    }

    [Fact]
    public void AddCallType_TakesACaseVariant_AsAlreadyThere()
    {
        // An admin's "call" is the same type, not a reason for a second one.
        var taskTypes = new SettingDescriptor { Name = TaskManagementSettings.TaskTypes.Name, AllowedValues = ["call"] };

        SalesRepTaskTypeSeeder.AddCallType(taskTypes);

        taskTypes.AllowedValues.Should().Equal("call");
    }

    [Fact]
    public async Task EnsureCallType_AppendsToASavedListWithoutIt_KeepingTheAdminsValues()
    {
        // The case behind the startup step: an admin saved the dictionary (removing "Call", or before it existed),
        // so the built-in list no longer counts and only the saved one can carry it.
        var settings = new FakeSettingsManager { Saved = ["Other", "Visit"] };

        await CreateSeeder(settings).EnsureCallTypeAsync();

        settings.Writes.Should().ContainSingle().Which.AllowedValues.Should().Equal("Other", "Visit", "Call");
    }

    [Fact]
    public async Task EnsureCallType_LeavesASavedListThatHasIt()
    {
        var settings = new FakeSettingsManager { Saved = ["Visit", "call"] };

        await CreateSeeder(settings).EnsureCallTypeAsync();

        settings.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureCallType_NeverSavesTheBuiltInList()
    {
        // Nothing saved yet: the built-in list is live, and saving it - even with "Call" missing from it - would turn
        // it into a saved one for good. Module.Initialize puts "Call" into the built-in list instead.
        var settings = new FakeSettingsManager();

        await CreateSeeder(settings).EnsureCallTypeAsync();

        settings.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureCallType_LeavesAListSetInConfiguration()
    {
        // A value forced through appsettings wins over both lists and the platform ignores saves to it.
        var settings = new FakeSettingsManager { Saved = ["Other"], ReadOnly = true };

        await CreateSeeder(settings).EnsureCallTypeAsync();

        settings.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureCallType_DoesNothingWithoutTaskManagement()
    {
        // TaskManagement is an optional dependency: absent, its setting is not registered, and reading it would throw.
        var settings = new FakeSettingsManager { Registered = false };

        await CreateSeeder(settings).EnsureCallTypeAsync();

        settings.Reads.Should().Be(0);
        settings.Writes.Should().BeEmpty();
    }

    private static SalesRepTaskTypeSeeder CreateSeeder(ISettingsManager settings) =>
        new(settings, NullLogger<SalesRepTaskTypeSeeder>.Instance);

    /// <summary>
    /// The one dictionary setting, the way the platform serves it: with the saved row's Id and values once an admin
    /// has saved it, without an Id before. Saves are recorded, not applied.
    /// </summary>
    private sealed class FakeSettingsManager : ISettingsManager
    {
        public bool Registered { get; init; } = true;
        public bool ReadOnly { get; init; }
        public object[] Saved { get; init; }

        public int Reads { get; private set; }
        public List<ObjectSettingEntry> Writes { get; } = [];

        private SettingDescriptor Descriptor => new()
        {
            Name = TaskManagementSettings.TaskTypes.Name,
            IsDictionary = true,
        };

        public IEnumerable<SettingDescriptor> AllRegisteredSettings => Registered ? [Descriptor] : [];

        public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
        {
            Reads++;

            var setting = new ObjectSettingEntry(Descriptor) { IsReadOnly = ReadOnly };
            if (Saved != null)
            {
                setting.Id = "saved-setting";
                setting.AllowedValues = Saved.ToArray();
            }

            return Task.FromResult(setting);
        }

        public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            Writes.AddRange(objectSettings);
            return Task.CompletedTask;
        }

        public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null) { }
        public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName) { }
        public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName) => [];
        public IDictionary<string, string[]> GetSettingTypeAssignments() => new Dictionary<string, string[]>();

        public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
            => Task.FromResult<IEnumerable<ObjectSettingEntry>>([]);

        public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
    }
}
