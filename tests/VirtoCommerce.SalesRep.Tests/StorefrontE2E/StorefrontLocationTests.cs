using System;
using System.IO;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E;

/// <summary>
/// Where the storefront tests find their storefront, on a ticket folder laid out on disk: this repo and a clone in
/// <c>sources</c>, a build in <c>front</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StorefrontLocationTests : IDisposable
{
    private readonly string _ticket = Path.Combine(Path.GetTempPath(), $"storefront-location-{Guid.NewGuid():N}");

    public StorefrontLocationTests()
    {
        Directory.CreateDirectory(Repo);
    }

    private string Repo => Path.Combine(_ticket, "sources", "vc-module-sales-rep");

    private string Clone => Path.Combine(_ticket, "sources", "vc-frontend");

    private string Build => Path.Combine(_ticket, "front");

    public void Dispose() => Directory.Delete(_ticket, recursive: true);

    [Fact]
    public void Resolve_TakesTheTicketsClone_OverItsBuild()
    {
        CreateClone(Clone, installed: true);
        CreateBuild(Build);

        var storefront = StorefrontLocation.Resolve(null, null, Repo);

        storefront.SourceDirectory.Should().Be(Clone);
        storefront.BuildDirectory.Should().BeNull();
    }

    [Fact]
    public void Resolve_FailsOnAnUninstalledClone_InsteadOfFallingBackToTheBuild()
    {
        // A ticket that moved to a clone keeps its old front folder.
        CreateClone(Clone, installed: false);
        CreateBuild(Build);

        var resolve = () => StorefrontLocation.Resolve(null, null, Repo);

        resolve.Should().Throw<InvalidOperationException>().WithMessage($"*{Clone}*yarn install*");
    }

    [Fact]
    public void Resolve_TakesTheTicketsBuild_WithoutAClone()
    {
        CreateBuild(Build);

        var storefront = StorefrontLocation.Resolve(null, null, Repo);

        storefront.BuildDirectory.Should().Be(Build);
        storefront.SourceDirectory.Should().BeNull();
    }

    [Fact]
    public void Resolve_FailsWithoutAStorefront_NamingWhereItLooked()
    {
        var resolve = () => StorefrontLocation.Resolve(null, null, Repo);

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{Clone}*{Build}*{StorefrontLocation.SourceVariable}*{StorefrontLocation.BuildVariable}*");
    }

    [Fact]
    public void Resolve_ServesAConfiguredBuild_WithTheCloneItCameFrom()
    {
        // A CI job: the clone it built, the build inside it. The ticket's own clone does not count then.
        var clone = Path.Combine(_ticket, "vc-frontend");
        var build = Path.Combine(clone, "artifacts", "default");
        CreateClone(clone, installed: true);
        CreateBuild(build);
        CreateClone(Clone, installed: true);

        var storefront = StorefrontLocation.Resolve(build, clone, Repo);

        storefront.BuildDirectory.Should().Be(build);
        storefront.SourceDirectory.Should().Be(clone);
    }

    [Fact]
    public void Resolve_FailsOnAConfiguredFolderThatIsNotABuild()
    {
        var resolve = () => StorefrontLocation.Resolve(Repo, null, Repo);

        resolve.Should().Throw<InvalidOperationException>().WithMessage($"*{StorefrontLocation.BuildVariable}*{Repo}*index.html*");
    }

    private static void CreateClone(string directory, bool installed)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "package.json"), "{}");

        if (installed)
        {
            var vite = ViteDevServer.EntryPoint(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(vite));
            File.WriteAllText(vite, string.Empty);
        }
    }

    private static void CreateBuild(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.html"), "<!doctype html>");
    }
}
