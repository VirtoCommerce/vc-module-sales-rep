using System;
using System.Collections.Generic;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.Data.Services;

// On the FileSystem assets provider this hides "assets/sales-rep-documents" from anonymous static file serving.
// On Azure the returned path is inert: the blob host serves the bytes and the private container is the guard there.
public class SalesRepDocumentsProtectedPathsSource : IProtectedStaticPathsSource
{
    private readonly IBlobUrlResolver _blobUrlResolver;

    public SalesRepDocumentsProtectedPathsSource(IBlobUrlResolver blobUrlResolver)
    {
        _blobUrlResolver = blobUrlResolver;
    }

    public IEnumerable<string> GetPaths()
    {
        string path;

        try
        {
            var url = _blobUrlResolver.GetAbsoluteUrl(ModuleConstants.DocumentsScope);
            path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        }
        catch
        {
            // A misconfigured assets provider must not break platform startup.
            yield break;
        }

        yield return path;
    }
}
