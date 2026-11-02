using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Data.Services;
using TaskManagementSettings = VirtoCommerce.TaskManagement.Core.ModuleConstants.Settings.General;
using XapiModuleConstants = VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The harness's minimal settings double plus the storefront-relevant values: ReturnModuleVersion on, so the store
/// query appends the installed modules without public settings (task management) to store.settings.modules, and the
/// task types the hub's task form offers.
/// </summary>
internal sealed class StorefrontSettingsManager : ISettingsManager
{
    public IEnumerable<SettingDescriptor> AllRegisteredSettings => [];
    public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null) { }
    public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName) { }
    public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName) => [];
    public IDictionary<string, string[]> GetSettingTypeAssignments() => new Dictionary<string, string[]>();

    public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
    {
        var setting = new ObjectSettingEntry
        {
            Name = name,
            // Non-null AllowedValues everywhere: FileExtensionService unions the white/blacklist settings' AllowedValues.
            AllowedValues = name == PlatformConstants.Settings.General.Languages.Name ? ["en-US", "de-DE"] : [],
        };

        if (name == XapiModuleConstants.Settings.General.ReturnModuleVersion.Name)
        {
            setting.ValueType = SettingValueType.Boolean;
            setting.Value = true;
        }

        if (name == TaskManagementSettings.TaskTypes.Name)
        {
            // The built-in list with "Call" added, as the module's Initialize does - on a copy, since the descriptor
            // is task management's static one.
            var taskTypes = new SettingDescriptor { Name = name, AllowedValues = TaskManagementSettings.TaskTypes.AllowedValues.ToArray() };
            SalesRepTaskTypeSeeder.AddCallType(taskTypes);
            setting.AllowedValues = taskTypes.AllowedValues;
        }

        return Task.FromResult(setting);
    }

    public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
        => Task.FromResult<IEnumerable<ObjectSettingEntry>>([]);

    public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
    public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
}
