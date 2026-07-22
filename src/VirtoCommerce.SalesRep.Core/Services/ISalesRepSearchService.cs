using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Paged search over the Sales Reps (contacts holding the sales-rep role), with keyword / sort / filter support.
/// Implements the platform <see cref="ISearchService{TCriteria, TResult, TModel}"/> contract, so the standard
/// <c>SearchAllAsync</c> / <c>SearchNoCloneAsync</c> extensions apply. The search builds a fresh result each call
/// (no shared cache), so the <c>clone</c> flag is a no-op.
/// </summary>
public interface ISalesRepSearchService : ISearchService<SalesRepSearchCriteria, SalesRepSearchResult, SalesRepListItem>
{
}
