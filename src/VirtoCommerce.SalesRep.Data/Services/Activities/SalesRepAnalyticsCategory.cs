using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public sealed record SalesRepAnalyticsCategory(string Category, string Type, IList<string> EventNames, IList<string> DimensionNames);
