using System;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

internal sealed record FoldedStatistics(decimal Total, int Count, decimal Average, DateTime? EarliestDate, DateTime? LatestDate, string CurrencyCode, string Warning);
