using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCartStatisticsResponseGroupParser : ISalesRepCartStatisticsResponseGroupParser
{
    private static readonly string[] _itemQuantityFields =
    [
        nameof(CustomerCartStatisticsPeriod.SelectedItemQuantity),
        nameof(CustomerCartStatisticsPeriod.UnselectedItemQuantity),
        nameof(CustomerCartStatisticsComparison.SelectedItemQuantityChange),
        nameof(CustomerCartStatisticsComparison.SelectedItemQuantityChangePercent),
        nameof(CustomerCartStatisticsComparison.UnselectedItemQuantityChange),
        nameof(CustomerCartStatisticsComparison.UnselectedItemQuantityChangePercent),
    ];

    private static readonly string[] _cartFigureFields =
    [
        nameof(CustomerCartStatisticsPeriod.Count),
        nameof(CustomerCartStatisticsPeriod.Total),
        nameof(CustomerCartStatisticsPeriod.Average),
        nameof(CustomerCartStatisticsPeriod.Warning),
        nameof(CustomerCartStatisticsComparison.CountChange),
        nameof(CustomerCartStatisticsComparison.CountChangePercent),
        nameof(CustomerCartStatisticsComparison.TotalChange),
        nameof(CustomerCartStatisticsComparison.TotalChangePercent),
        nameof(CustomerCartStatisticsComparison.AverageChange),
        nameof(CustomerCartStatisticsComparison.AverageChangePercent),
    ];

    public virtual CartStatisticsResponseGroup GetResponseGroup(IList<string> includeFields)
    {
        var result = CartStatisticsResponseGroup.None;

        if (_itemQuantityFields.Any(includeFields.IncludesField))
        {
            result |= CartStatisticsResponseGroup.ItemQuantities;
        }

        if (_cartFigureFields.Any(includeFields.IncludesField))
        {
            result |= CartStatisticsResponseGroup.CartFigures;
        }

        return result;
    }
}
