using System;
using System.IO;
using System.Linq;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// The BUILT sales-rep VC-Shell app (index.html plus its bundles) the tests serve, found once per run:
/// <c>VC_VCSHELL_APP_DIR</c>, else <c>App/dist</c> (what <c>yarn build:app</c> in
/// <c>src/VirtoCommerce.SalesRep.Web/App</c> writes), else <c>Content/vc-sales-rep</c> (where vc-build copies it).
/// Without one the tests fail with the command that builds it.
/// </summary>
internal static class VCShellAppLocation
{
    public const string Variable = "VC_VCSHELL_APP_DIR";

    private static readonly Lazy<string> _appDirectory = new(Resolve);

    /// <summary>The built app, or an exception naming what is missing.</summary>
    public static string AppDirectory => _appDirectory.Value;

    private static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrEmpty(configured))
        {
            return IsBuiltApp(configured)
                ? configured
                : throw new InvalidOperationException($"{Variable} names {configured}, which is not a built VC-Shell app: it has no index.html with an index*.js bundle.");
        }

        // bin/Debug/net10.0 -> tests project -> tests -> repo
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var web = Path.Combine(repo, "src", "VirtoCommerce.SalesRep.Web");
        var app = Path.Combine(web, "App");

        return new[] { Path.Combine(app, "dist"), Path.Combine(web, "Content", "vc-sales-rep") }.FirstOrDefault(IsBuiltApp)
            ?? throw new InvalidOperationException($"The VC-Shell app is not built: run `yarn build:app` in {app}, or set {Variable} to a built app.");
    }

    // A placeholder Content/<app-id>/index.html (committed so the platform boots) has no bundle next to it.
    private static bool IsBuiltApp(string directory)
        => File.Exists(Path.Combine(directory, "index.html"))
           && Directory.EnumerateFiles(directory, "index*.js").Any();
}
