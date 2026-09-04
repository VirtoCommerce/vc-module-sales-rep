using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Caching;

namespace VirtoCommerce.SalesRep.Data.Handlers;

// The hub's cart figures are aggregated straight from the carts table, so a cart change is the only thing that can
// move them. The event is published after the commit and the in-process bus awaits its handlers, so by the time a
// mutation answers, the rep's own edit is already gone from this instance's cache; other instances are reached by the
// Redis backplane, which carries the expirations.
public class SalesRepStatisticsCartChangedEventHandler : IEventHandler<CartChangedEvent>
{
    private readonly ISettingsManager _settingsManager;

    public SalesRepStatisticsCartChangedEventHandler(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public virtual Task Handle(CartChangedEvent message)
    {
        // Both sides of a move: a cart that changed organization has to leave the figures of the one it came from.
        var organizationIds = message.ChangedEntries
            .SelectMany(x => new[] { x.OldEntry?.OrganizationId, x.NewEntry?.OrganizationId })
            .Where(x => !string.IsNullOrEmpty(x))
            .DistinctIgnoreCase()
            .ToList();

        return StatisticsCacheInvalidation.ExpireAsync(
            _settingsManager, [ModuleConstants.Settings.Caching.Families.Cart], organizationIds);
    }
}
