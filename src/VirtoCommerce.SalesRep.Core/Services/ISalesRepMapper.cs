using System.Collections.Generic;
using VirtoCommerce.SalesRep.Core.Models;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepMapper
{
    SalesRepDocument ToDocument(File file, SalesRepDocumentMetadata metadata);

    // Pairs metadata rows with their library files; rows whose file is missing or outside the library scope are skipped.
    IList<SalesRepDocument> ToDocuments(IList<File> files, IList<SalesRepDocumentMetadata> metadataItems);
}
