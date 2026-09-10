using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Events;

public class DocumentMetadataChangingEvent : GenericChangedEntryEvent<SalesRepDocumentMetadata>
{
    public DocumentMetadataChangingEvent(IEnumerable<GenericChangedEntry<SalesRepDocumentMetadata>> changedEntries)
        : base(changedEntries)
    {
    }
}
