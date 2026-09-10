using System.Collections.Generic;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Models.Facets;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepMapper
{
    SalesRepDocument ToDocument(File file, SalesRepDocumentMetadata metadata);

    // The metadata list is authoritative: every row maps; a missing or foreign-scope file only degrades the
    // file-derived fields to null.
    IList<SalesRepDocument> ToDocuments(IList<File> files, IList<SalesRepDocumentMetadata> metadataItems);

    SalesRepDocumentMetadata ToMetadata(SalesRepDocument document);

    IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName);
}
