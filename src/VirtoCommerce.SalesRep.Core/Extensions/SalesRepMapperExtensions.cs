using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Core.Extensions;

public static class SalesRepMapperExtensions
{
    // The single home of the fetch-then-map contract: only the page's files are fetched, no-clone.
    public static async Task<IList<SalesRepDocument>> ToDocumentsAsync(
        this ISalesRepMapper mapper,
        IFileUploadService fileUploadService,
        IList<SalesRepDocumentMetadata> metadataItems)
    {
        if (metadataItems.Count == 0)
        {
            return [];
        }

        var files = await fileUploadService.GetAsync(metadataItems.Select(x => x.FileId).ToList(), clone: false);

        return mapper.ToDocuments(files, metadataItems);
    }
}
