using System;
using System.IO;
using System.Linq;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The hub as the storefront gets it in production: the module's Module Federation plugin, which <c>yarn build</c> in
/// <c>StorefrontApp</c> writes to <c>plugins/vc-frontend</c>. The tests serve that build as it is. A missing or outdated
/// one fails the group with the command to run: a rebuild per run would cost 30-60 seconds, and testing a stale
/// build would test yesterday's hub.
/// </summary>
internal static class StorefrontPlugin
{
    /// <summary>The platform's static-file route for the module's plugin; AppManifestService builds its URLs on it.</summary>
    public const string RequestPath = "/modules/$(VirtoCommerce.SalesRep)/plugins/vc-frontend";

    /// <summary>The module's root as the platform sees it (its FullPhysicalPath): the Web project.</summary>
    public static string ModuleDirectory { get; } = Path.GetFullPath(
        // bin/Debug/net10.0 -> tests project -> tests -> repo
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "VirtoCommerce.SalesRep.Web"));

    public static string BuildDirectory => Path.Combine(ModuleDirectory, "plugins", "vc-frontend");

    private static string AppDirectory => Path.Combine(ModuleDirectory, "StorefrontApp");

    public static void EnsureBuilt()
    {
        // Written on every build, so its time is the build's.
        var build = new FileInfo(Path.Combine(BuildDirectory, "mf-manifest.json"));
        if (!build.Exists)
        {
            throw new InvalidOperationException($"The storefront plugin is not built: run `yarn build` in {AppDirectory}.");
        }

        // Tests are not part of the bundle; anything else under src is.
        var newest = new DirectoryInfo(Path.Combine(AppDirectory, "src"))
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(x => !x.Name.Contains(".test."))
            .MaxBy(x => x.LastWriteTimeUtc);

        if (newest != null && newest.LastWriteTimeUtc > build.LastWriteTimeUtc)
        {
            throw new InvalidOperationException(
                $"The storefront plugin build ({build.LastWriteTime:g}) is older than {Path.GetRelativePath(AppDirectory, newest.FullName)} " +
                $"({newest.LastWriteTime:g}): run `yarn build` in {AppDirectory}.");
        }
    }
}
