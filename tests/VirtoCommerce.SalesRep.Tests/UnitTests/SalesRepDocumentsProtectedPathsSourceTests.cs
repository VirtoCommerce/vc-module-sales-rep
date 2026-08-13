using System;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

[Trait("Category", "Unit")]
public class SalesRepDocumentsProtectedPathsSourceTests
{
    [Fact]
    public void GetPaths_RelativeUrl_YieldsAsIs()
    {
        var source = CreateSource(_ => $"assets/{ModuleConstants.DocumentsScope}");

        source.GetPaths().Should().Equal($"assets/{ModuleConstants.DocumentsScope}");
    }

    [Fact]
    public void GetPaths_AbsoluteUrl_YieldsAbsolutePath()
    {
        var source = CreateSource(_ => $"https://localhost:5001/assets/{ModuleConstants.DocumentsScope}");

        source.GetPaths().Should().Equal($"/assets/{ModuleConstants.DocumentsScope}");
    }

    [Fact]
    public void GetPaths_ResolverThrows_YieldsNothing()
    {
        var source = CreateSource(_ => throw new InvalidOperationException("Assets provider is misconfigured"));

        source.GetPaths().ToList().Should().BeEmpty();
    }

    private static SalesRepDocumentsProtectedPathsSource CreateSource(Func<string, string> getAbsoluteUrl)
    {
        return new SalesRepDocumentsProtectedPathsSource(new StubBlobUrlResolver(getAbsoluteUrl));
    }

    private sealed class StubBlobUrlResolver : IBlobUrlResolver
    {
        private readonly Func<string, string> _getAbsoluteUrl;

        public StubBlobUrlResolver(Func<string, string> getAbsoluteUrl)
        {
            _getAbsoluteUrl = getAbsoluteUrl;
        }

        public string GetAbsoluteUrl(string blobKey)
        {
            return _getAbsoluteUrl(blobKey);
        }
    }
}
