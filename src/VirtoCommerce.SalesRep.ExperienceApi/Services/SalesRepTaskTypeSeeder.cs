using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using TaskManagementSettings = VirtoCommerce.TaskManagement.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

// The platform serves the built-in TaskManagement.TaskTypes list until an admin first saves the dictionary, and only the
// saved list after that - never both. AddCallType keeps "Call" in the first, EnsureCallTypeAsync in the second.
public class SalesRepTaskTypeSeeder(ISettingsManager settingsManager, ILogger<SalesRepTaskTypeSeeder> logger) : ISalesRepTaskTypeSeeder
{
    public static void AddCallType(SettingDescriptor taskTypes)
    {
        if (!HasCallType(taskTypes.AllowedValues))
        {
            taskTypes.AllowedValues = [.. taskTypes.AllowedValues ?? [], ModuleConstants.Tasks.CallType];
        }
    }

    public virtual async Task EnsureCallTypeAsync()
    {
        var name = TaskManagementSettings.TaskTypes.Name;

        if (!settingsManager.AllRegisteredSettings.Any(x => x.Name.EqualsIgnoreCase(name)))
        {
            return;
        }

        var setting = await settingsManager.GetObjectSettingAsync(name);

        if (setting.IsReadOnly)
        {
            logger.LogWarning("{SettingName} is set in configuration, so the {TaskType} task type cannot be added to it.", name, ModuleConstants.Tasks.CallType);
            return;
        }

        // No Id = nothing saved, the built-in list is served. Saving it here would freeze that list.
        if (string.IsNullOrEmpty(setting.Id) || HasCallType(setting.AllowedValues))
        {
            return;
        }

        AddCallType(setting);
        await settingsManager.SaveObjectSettingsAsync([setting]);
    }

    private static bool HasCallType(object[] values)
    {
        return values?.OfType<string>().Any(x => x.EqualsIgnoreCase(ModuleConstants.Tasks.CallType)) == true;
    }
}
