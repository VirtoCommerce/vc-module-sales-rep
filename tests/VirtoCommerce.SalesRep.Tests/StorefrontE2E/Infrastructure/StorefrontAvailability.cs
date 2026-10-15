using System;
using System.IO;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// Whether the storefront tests can run on this machine. They need the vc-frontend checkout (its Vite dev server
/// and its GraphQL documents), which exists in a developer workspace but not on a CI runner, so the tests skip
/// themselves instead of failing there: <c>[Fact(Skip = …, SkipUnless = nameof(IsAvailable), SkipType = …)]</c>.
/// Resolution order: <c>VC_STOREFRONT_DIR</c>, else <c>&lt;workspace&gt;/front</c> next to
/// <c>&lt;workspace&gt;/vc-sources/&lt;this repo&gt;</c>. <c>VC_STOREFRONT_E2E=0</c> opts out explicitly.
/// </summary>
public static class StorefrontAvailability
{
    public const string SkipReason = "Storefront E2E needs the vc-frontend checkout (VC_STOREFRONT_DIR or <workspace>/front) and VC_STOREFRONT_E2E not set to 0.";

    private static readonly Lazy<string> _frontendDirectory = new(Resolve);

    public static bool IsAvailable => _frontendDirectory.Value != null;

    /// <summary>The checkout, or an exception naming what is missing.</summary>
    public static string FrontendDirectory
        => _frontendDirectory.Value ?? throw new InvalidOperationException(SkipReason);

    private static string Resolve()
    {
        var optOut = Environment.GetEnvironmentVariable("VC_STOREFRONT_E2E");
        if (optOut is "0" or "false" or "False")
        {
            return null;
        }

        var configured = Environment.GetEnvironmentVariable("VC_STOREFRONT_DIR");
        if (!string.IsNullOrEmpty(configured))
        {
            return IsCheckout(configured) ? configured : null;
        }

        // bin/Debug/net10.0 -> tests project -> tests -> repo -> vc-sources -> workspace
        var workspace = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(workspace, "front");

        return IsCheckout(candidate) ? candidate : null;
    }

    private static bool IsCheckout(string directory)
        => File.Exists(Path.Combine(directory, "package.json"))
           && File.Exists(Path.Combine(directory, "node_modules", "vite", "bin", "vite.js"));
}
