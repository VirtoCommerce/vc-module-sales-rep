using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Web;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>The module under test, as the platform loads it: its Web project's <c>module.manifest</c>, rooted there.</summary>
internal static class TestedModule
{
    /// <summary>
    /// The module's root as the platform sees it (its FullPhysicalPath): the Web project, where the notification
    /// templates and the storefront plugin's build are.
    /// </summary>
    public static string Directory { get; } = Path.GetFullPath(
        // bin/Debug/net10.0 -> tests project -> tests -> repo
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "VirtoCommerce.SalesRep.Web"));

    /// <summary>What the platform reads from the manifest: the module's id, version, dependencies and apps.</summary>
    public static ManifestModuleInfo CreateInfo()
    {
        var info = new ManifestModuleInfo().LoadFromManifest(ManifestReader.Read(Path.Combine(Directory, "module.manifest")));
        info.FullPhysicalPath = Directory;

        return info;
    }

    /// <summary>A new <see cref="Module"/> with its manifest's info and the configuration, as the platform creates it.</summary>
    public static Module Create(IConfiguration configuration) => new() { ModuleInfo = CreateInfo(), Configuration = configuration };
}
