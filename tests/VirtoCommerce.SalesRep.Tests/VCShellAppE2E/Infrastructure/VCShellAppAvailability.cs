using System;
using System.IO;
using System.Linq;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// Whether the VC-Shell app tests can run on this machine. They need the BUILT sales-rep VC-Shell app (index.html plus
/// its bundles), which a developer produces with <c>yarn build:app</c> in <c>src/VirtoCommerce.SalesRep.Web/App</c>
/// and vc-build copies to <c>Content/vc-sales-rep</c>; a CI runner without either skips the tests instead of
/// failing: <c>[Fact(Skip = …, SkipUnless = nameof(IsAvailable), SkipType = …)]</c>.
/// Resolution order: <c>VC_VCSHELL_APP_DIR</c>, else <c>App/dist</c>, else <c>Content/vc-sales-rep</c> in this repo.
/// <c>VC_VCSHELL_APP_E2E=0</c> opts out explicitly.
/// </summary>
public static class VCShellAppAvailability
{
    public const string SkipReason = "VC-Shell app E2E needs the built VC-Shell app (VC_VCSHELL_APP_DIR, src/VirtoCommerce.SalesRep.Web/App/dist or Content/vc-sales-rep) and VC_VCSHELL_APP_E2E not set to 0.";

    private static readonly Lazy<string> _appDirectory = new(Resolve);

    public static bool IsAvailable => _appDirectory.Value != null;

    /// <summary>The built app, or an exception naming what is missing.</summary>
    public static string AppDirectory
        => _appDirectory.Value ?? throw new InvalidOperationException(SkipReason);

    private static string Resolve()
    {
        var optOut = Environment.GetEnvironmentVariable("VC_VCSHELL_APP_E2E");
        if (optOut is "0" or "false" or "False")
        {
            return null;
        }

        var configured = Environment.GetEnvironmentVariable("VC_VCSHELL_APP_DIR");
        if (!string.IsNullOrEmpty(configured))
        {
            return IsBuiltApp(configured) ? configured : null;
        }

        // bin/Debug/net10.0 -> tests project -> tests -> repo
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var web = Path.Combine(repo, "src", "VirtoCommerce.SalesRep.Web");

        return new[] { Path.Combine(web, "App", "dist"), Path.Combine(web, "Content", "vc-sales-rep") }
            .FirstOrDefault(IsBuiltApp);
    }

    // A placeholder Content/<app-id>/index.html (committed so the platform boots) has no bundle next to it.
    private static bool IsBuiltApp(string directory)
        => File.Exists(Path.Combine(directory, "index.html"))
           && Directory.Exists(directory)
           && Directory.EnumerateFiles(directory, "index*.js").Any();
}
