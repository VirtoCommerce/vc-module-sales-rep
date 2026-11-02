using System;
using System.IO;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The storefront the tests run against, found once per run. A vc-frontend clone runs on its own Vite dev server; a
/// built storefront (the theme zip's <c>default</c> folder: a release or a PR build) is served by the in-process
/// backend from its own origin, the way the cloud serves it. Looked up in this order:
/// <list type="number">
/// <item><c>VC_STOREFRONT_BUILD</c>, a build. <c>VC_STOREFRONT_DIR</c> may name the clone it was built from, whose
/// <c>.graphql</c> documents the contract test reads.</item>
/// <item><c>VC_STOREFRONT_DIR</c>, a clone.</item>
/// <item>A <c>vc-frontend</c> clone next to this repo, as in a ticket folder's <c>sources</c>.</item>
/// <item>A build two levels up, as in a ticket folder's <c>front</c>.</item>
/// </list>
/// Nothing found, or a storefront that cannot run, fails the tests with what is missing. A clone that is there wins
/// even when it cannot run: a ticket that moved to a clone keeps its old <c>front</c> folder.
/// </summary>
internal sealed class StorefrontLocation
{
    public const string BuildVariable = "VC_STOREFRONT_BUILD";
    public const string SourceVariable = "VC_STOREFRONT_DIR";

    private static readonly Lazy<StorefrontLocation> _current = new(() => Resolve(
        Environment.GetEnvironmentVariable(BuildVariable),
        Environment.GetEnvironmentVariable(SourceVariable),
        // the module's Web project -> src -> repo
        Path.GetFullPath(Path.Combine(StorefrontPlugin.ModuleDirectory, "..", ".."))));

    private StorefrontLocation(string buildDirectory, string sourceDirectory)
    {
        BuildDirectory = buildDirectory;
        SourceDirectory = sourceDirectory;
    }

    /// <summary>This run's storefront, or an exception naming what is missing.</summary>
    public static StorefrontLocation Current => _current.Value;

    /// <summary>The built storefront the backend serves; null when a clone runs on Vite.</summary>
    public string BuildDirectory { get; }

    /// <summary>The clone: what Vite runs when there is no build, and where the <c>.graphql</c> documents are. Null for a build alone.</summary>
    public string SourceDirectory { get; }

    internal static StorefrontLocation Resolve(string configuredBuild, string configuredSource, string repositoryDirectory)
    {
        if (!string.IsNullOrEmpty(configuredBuild))
        {
            RequireBuild(configuredBuild);
            if (string.IsNullOrEmpty(configuredSource))
            {
                return new StorefrontLocation(configuredBuild, null);
            }

            RequireClone(configuredSource);
            return new StorefrontLocation(configuredBuild, configuredSource);
        }

        if (!string.IsNullOrEmpty(configuredSource))
        {
            RequireClone(configuredSource);
            RequireInstalled(configuredSource);
            return new StorefrontLocation(null, configuredSource);
        }

        var clone = Path.GetFullPath(Path.Combine(repositoryDirectory, "..", "vc-frontend"));
        if (File.Exists(Path.Combine(clone, "package.json")))
        {
            RequireInstalled(clone);
            return new StorefrontLocation(null, clone);
        }

        var build = Path.GetFullPath(Path.Combine(repositoryDirectory, "..", "..", "front"));
        if (File.Exists(Path.Combine(build, "index.html")))
        {
            return new StorefrontLocation(build, null);
        }

        throw new InvalidOperationException(
            $"No storefront found: no vc-frontend clone at {clone} and no built storefront at {build}. " +
            $"Set {SourceVariable} to a clone or {BuildVariable} to a build (the theme zip's default folder).");
    }

    private static void RequireBuild(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "index.html")))
        {
            throw new InvalidOperationException(
                $"{BuildVariable} names {directory}, which is not a built storefront: it has no index.html (a build is the theme zip's default folder).");
        }
    }

    private static void RequireClone(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "package.json")))
        {
            throw new InvalidOperationException($"{SourceVariable} names {directory}, which is not a vc-frontend clone: it has no package.json.");
        }
    }

    /// <summary>A clone runs on its own Vite dev server, which needs the clone's packages.</summary>
    private static void RequireInstalled(string directory)
    {
        if (!File.Exists(ViteDevServer.EntryPoint(directory)))
        {
            throw new InvalidOperationException($"The vc-frontend clone at {directory} has no node_modules: run `yarn install` there.");
        }
    }
}
