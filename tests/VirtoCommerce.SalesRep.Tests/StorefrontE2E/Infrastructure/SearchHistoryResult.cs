using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class SearchHistoryResult
{
    public IList<string> Queries { get; set; } = [];
}
