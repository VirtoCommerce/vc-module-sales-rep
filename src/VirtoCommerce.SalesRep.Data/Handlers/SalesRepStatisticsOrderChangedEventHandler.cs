using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Caching;
using LineItemSignature = (string Id, string ProductId, string Sku, string Name, string ImageUrl,
    string CategoryId, string Currency, decimal Price, int Quantity, bool IsCancelled);

namespace VirtoCommerce.SalesRep.Data.Handlers;

// Order figures, the status vocabulary, the ordering-customer count and the top-seller ranking all aggregate the
// orders table, so an order change is what moves them.
public class SalesRepStatisticsOrderChangedEventHandler : IEventHandler<OrderChangedEvent>
{
    private readonly ISettingsManager _settingsManager;

    public SalesRepStatisticsOrderChangedEventHandler(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public virtual Task Handle(OrderChangedEvent message)
    {
        // Both sides of a move: an order that changed organization has to leave the figures of the one it came from.
        var organizationIds = message.ChangedEntries
            .Where(IsAggregateRelevant)
            .SelectMany(x => new[] { x.OldEntry?.OrganizationId, x.NewEntry?.OrganizationId })
            .Where(x => !string.IsNullOrEmpty(x))
            .DistinctIgnoreCase()
            .ToList();

        return StatisticsCacheInvalidation.ExpireAsync(
            _settingsManager, ModuleConstants.Settings.Caching.Families.OrderDriven, organizationIds);
    }

    // The status pipeline saves an order repeatedly; only a change an aggregate actually reads is worth a recompute.
    protected virtual bool IsAggregateRelevant(GenericChangedEntry<CustomerOrder> entry)
    {
        var oldEntry = entry.OldEntry;
        var newEntry = entry.NewEntry;

        if (entry.EntryState != EntryState.Modified || oldEntry == null || newEntry == null)
        {
            return true;
        }

        return oldEntry.OrganizationId != newEntry.OrganizationId ||
            oldEntry.CustomerId != newEntry.CustomerId ||
            oldEntry.StoreId != newEntry.StoreId ||
            oldEntry.Status != newEntry.Status ||
            oldEntry.Currency != newEntry.Currency ||
            oldEntry.Total != newEntry.Total ||
            oldEntry.IsCancelled != newEntry.IsCancelled ||
            oldEntry.IsPrototype != newEntry.IsPrototype ||
            oldEntry.CreatedDate != newEntry.CreatedDate ||
            !GetLineItemSignatures(oldEntry).SetEquals(GetLineItemSignatures(newEntry));
    }

    // Top sellers reads the line items down to the display columns, so their values are part of what the aggregates
    // see. A set, so collection order doesn't matter — and values, never a joined string: OldEntry comes from the
    // database carrying the stored scale (12.0000) while NewEntry is the client payload, where JSON 12 has scale 0.
    // decimal.ToString() keeps that scale, so a string signature made every save look like a change (VCST-5755 F1).
    private static HashSet<LineItemSignature> GetLineItemSignatures(CustomerOrder order)
    {
        IEnumerable<LineItemSignature> signatures = order.Items?
            .Select(x => (x.Id, x.ProductId, x.Sku, x.Name, x.ImageUrl, x.CategoryId,
                x.Currency, x.Price, x.Quantity, x.IsCancelled))
            ?? [];

        return [.. signatures];
    }
}
