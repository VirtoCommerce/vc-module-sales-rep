using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepActivityService
{
    Task<SalesRepActivitySearchResult> SearchActivitiesAsync(SalesRepActivitySearchCriteria criteria);
}
