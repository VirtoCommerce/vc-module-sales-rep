using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Caching;

namespace VirtoCommerce.SalesRep.Data.Handlers;

// The event is published after the commit and the bus awaits its handlers, so the rep's own edit is already gone
// from this instance's cache by the time the mutation answers; other instances are reached by the Redis backplane.
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
