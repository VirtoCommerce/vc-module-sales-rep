using System;

namespace VirtoCommerce.SalesRep.Core.Models;

[Flags]
public enum CartStatisticsResponseGroup
{
    None = 0,
    ItemQuantities = 1,
    CartFigures = 1 << 1,
    Full = ItemQuantities | CartFigures,
}
