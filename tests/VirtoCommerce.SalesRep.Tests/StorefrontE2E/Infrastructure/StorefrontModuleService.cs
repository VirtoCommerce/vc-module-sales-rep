using System.Collections.Generic;
using System.IO;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The module list the store query reports to the storefront, and the one the platform's AppManifestService walks for
/// store.plugins. The module under test comes from its own module.manifest, the file the platform reads, so its id,
/// version and apps are the real ones; two of its dependencies stand in. Task management opens the storefront's tasks
/// pages (a presence check on the module id, the way the real module list reaches it with ReturnModuleVersion on), and
/// a group can leave it out; X-Frontend declares the storefront app whose plugins store.plugins lists. White labeling
/// is absent so the storefront drops its @gate'd field client-side.
/// </summary>
internal sealed class StorefrontModuleService(bool taskManagementInstalled = true) : IModuleService
{
    /// <summary>
    /// What the two stand-ins report: a version no storefront expects to exceed. The storefront compares each module's
    /// version with its own backend-packages.json, and a lower one gets its "outdated backend" error over the page's
    /// top-right buttons for fifteen seconds; a built storefront keeps that list inside its bundle. The module under
    /// test reports its real version, so a real mismatch still shows.
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
        var manifest = ManifestReader.Read(Path.Combine(StorefrontPlugin.ModuleDirectory, "module.manifest"));

        var salesRep = new ManifestModuleInfo().LoadFromManifest(manifest);
        // Where the platform looks for the hub plugin's build (plugins/vc-frontend).
        salesRep.FullPhysicalPath = StorefrontPlugin.ModuleDirectory;

        List<ManifestModuleInfo> modules =
        [
            salesRep,
            // X-Frontend's own manifest ships only in its module zip, so its app declaration is restated here.
            StandIn(manifest, "VirtoCommerce.XFrontend", new ManifestApp { Id = "vc-frontend" }),
        ];

        if (taskManagementInstalled)
        {
            modules.Add(StandIn(manifest, "VirtoCommerce.TaskManagement"));
        }

        return modules;
    }

    private static ManifestModuleInfo StandIn(ModuleManifest manifest, string id, params ManifestApp[] apps)
        => new ManifestModuleInfo().LoadFromManifest(new ModuleManifest { Id = id, Version = StandInVersion, PlatformVersion = manifest.PlatformVersion, Apps = apps });
}
