using System;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>Sum / count / average folded into one currency, plus that currency's code and the earliest/latest record date.</summary>
internal sealed record FoldedStatistics(decimal Total, int Count, decimal Average, DateTime? EarliestDate, DateTime? LatestDate, string CurrencyCode);
