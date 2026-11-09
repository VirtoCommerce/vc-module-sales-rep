using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The installed-module list, one of the two stand-ins the harnesses keep: the platform builds it by discovering module
/// folders at startup, which nothing here does. The store query reports it to the storefront (store.settings.modules),
/// the platform's AppManifestService walks it for store.plugins, and the translation service reads the installed
/// modules' translations from it. The module under test comes from its own module.manifest, the file the platform reads,
/// so its id, version and apps are the real ones and its root is the Web project, where its plugin build and
/// translations lie. Two of its dependencies stand in: task management, when the harness hosts it (the storefront opens
/// its tasks pages on the module id), and X-Frontend, which declares the storefront app whose plugins store.plugins lists.
/// White labeling is absent, so the storefront drops its @gate'd field client-side.
/// </summary>
internal sealed class InstalledModuleService(bool taskManagementInstalled) : IModuleService
{
    /// <summary>
    /// What the two stand-ins report: a version no storefront expects to exceed. The storefront compares each module's
    /// version with its own backend-packages.json, and a lower one gets its "outdated backend" error over the page's
    /// top-right buttons for fifteen seconds; a built storefront keeps that list inside its bundle. The module under test
    /// reports its real version, so a real mismatch still shows.
    /// </summary>
    private const string StandInVersion = "999.0.0";

    private readonly IList<ManifestModuleInfo> _installed = CreateInstalledModules(taskManagementInstalled);

    public IList<ManifestModuleInfo> GetModules() => _installed;

    public IList<ManifestModuleInfo> GetInstalledModules() => _installed;

    public IList<ManifestModuleInfo> GetFailedModules() => [];

    public bool IsInstalled(string moduleId) => GetModule(moduleId) != null;

    public bool IsInstalled(string moduleId, string minVersion) => IsInstalled(moduleId);

    public ManifestModuleInfo GetModule(string moduleId) => _installed.FirstOrDefault(x => x.Id.EqualsIgnoreCase(moduleId));

    private static List<ManifestModuleInfo> CreateInstalledModules(bool taskManagementInstalled)
    {
        // Loaded, as the platform marks a module it started: its translations are read from its root.
        var salesRep = TestedModule.CreateInfo();
        salesRep.State = ModuleState.Initialized;

        List<ManifestModuleInfo> modules =
        [
            salesRep,
            // X-Frontend's own manifest ships only in its module zip, so its app declaration is restated here.
            StandIn(salesRep, "VirtoCommerce.XFrontend", new ManifestApp { Id = "vc-frontend" }),
        ];

        if (taskManagementInstalled)
        {
            modules.Add(StandIn(salesRep, "VirtoCommerce.TaskManagement"));
        }

        return modules;
    }

    private static ManifestModuleInfo StandIn(ManifestModuleInfo salesRep, string id, params ManifestApp[] apps)
        => new ManifestModuleInfo().LoadFromManifest(new ModuleManifest
        {
            Id = id,
            Version = StandInVersion,
            PlatformVersion = salesRep.PlatformVersion.ToString(),
            Apps = apps,
        });
}
