using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Events;

public class DocumentMetadataChangedEvent : GenericChangedEntryEvent<SalesRepDocumentMetadata>
{
    public DocumentMetadataChangedEvent(IEnumerable<GenericChangedEntry<SalesRepDocumentMetadata>> changedEntries)
        : base(changedEntries)
    {
    }
}
