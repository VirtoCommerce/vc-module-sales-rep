using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepActivitySource
{
    IList<string> Categories { get; }

    Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria);
}
