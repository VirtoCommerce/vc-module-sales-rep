using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// In-memory <see cref="IBlobStorageProvider"/> + <see cref="IBlobUrlResolver"/> double: blobs live in a
/// dictionary keyed by relative URL, writes commit on stream dispose. Lets tests assert blob existence/content
/// (upload writes, delete cascade) without a file system.
/// </summary>
internal sealed class InMemoryBlobStorageProvider : IBlobStorageProvider, IBlobUrlResolver
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public IReadOnlyCollection<string> BlobUrls => [.. _blobs.Keys];

    /// <summary>When set, <see cref="RemoveAsync"/> throws this instead of deleting — a storage failure mid-delete.</summary>
    public Exception FailOnRemoveWith { get; set; }

    public bool Exists(string blobUrl) => _blobs.ContainsKey(Normalize(blobUrl));

    public Task<BlobEntrySearchResult> SearchAsync(string folderUrl, string keyword)
        => Task.FromResult(AbstractTypeFactory<BlobEntrySearchResult>.TryCreateInstance());

    public Task<BlobInfo> GetBlobInfoAsync(string blobUrl)
        => Task.FromResult(_blobs.TryGetValue(Normalize(blobUrl), out var bytes) ? CreateBlobInfo(blobUrl, bytes) : null);

    public Task CreateFolderAsync(BlobFolder folder) => Task.CompletedTask;

    public Stream OpenRead(string blobUrl)
        => _blobs.TryGetValue(Normalize(blobUrl), out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : throw new InvalidOperationException($"Blob '{blobUrl}' does not exist.");

    public Task<Stream> OpenReadAsync(string blobUrl) => Task.FromResult(OpenRead(blobUrl));

    public Stream OpenWrite(string blobUrl) => new CommitOnDisposeStream(this, Normalize(blobUrl));

    public Task<Stream> OpenWriteAsync(string blobUrl) => Task.FromResult(OpenWrite(blobUrl));

    public Task RemoveAsync(string[] urls)
    {
        if (FailOnRemoveWith != null)
        {
            throw FailOnRemoveWith;
        }

        foreach (var url in urls ?? [])
        {
            _blobs.TryRemove(Normalize(url), out _);
        }

        return Task.CompletedTask;
    }

    public void Move(string srcUrl, string destUrl)
    {
        if (_blobs.TryRemove(Normalize(srcUrl), out var bytes))
        {
            _blobs[Normalize(destUrl)] = bytes;
        }
    }

    public Task MoveAsyncPublic(string srcUrl, string destUrl)
    {
        Move(srcUrl, destUrl);
        return Task.CompletedTask;
    }

    public void Copy(string srcUrl, string destUrl)
    {
        if (_blobs.TryGetValue(Normalize(srcUrl), out var bytes))
        {
            _blobs[Normalize(destUrl)] = [.. bytes];
        }
    }

    public Task CopyAsync(string srcUrl, string destUrl)
    {
        Copy(srcUrl, destUrl);
        return Task.CompletedTask;
    }

    public string GetAbsoluteUrl(string blobKey) => $"memory://{Normalize(blobKey)}";

    private static string Normalize(string url) => url?.TrimStart('/');

    private static BlobInfo CreateBlobInfo(string blobUrl, byte[] bytes)
    {
        var blobInfo = AbstractTypeFactory<BlobInfo>.TryCreateInstance();
        blobInfo.Name = blobUrl.Split('/').Last();
        blobInfo.RelativeUrl = Normalize(blobUrl);
        blobInfo.Size = bytes.Length;
        return blobInfo;
    }

    private sealed class CommitOnDisposeStream : MemoryStream
    {
        private readonly InMemoryBlobStorageProvider _owner;
        private readonly string _blobUrl;
        private bool _committed;

        public CommitOnDisposeStream(InMemoryBlobStorageProvider owner, string blobUrl)
        {
            _owner = owner;
            _blobUrl = blobUrl;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_committed)
            {
                _committed = true;
                _owner._blobs[_blobUrl] = ToArray();
            }

            base.Dispose(disposing);
        }
    }
}
