using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepSearchService
{
    Task<SalesRepSearchResult> SearchAsync(SalesRepSearchCriteria criteria);
}
