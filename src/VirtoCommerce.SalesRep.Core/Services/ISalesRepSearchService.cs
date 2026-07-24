using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepSearchService : ISearchService<SalesRepSearchCriteria, SalesRepSearchResult, SalesRepListItem>
{
}
