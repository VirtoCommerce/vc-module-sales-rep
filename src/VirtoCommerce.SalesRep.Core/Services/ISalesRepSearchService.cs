using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Paged search over the Sales Reps (contacts holding the sales-rep role), with keyword / sort / filter support.
/// </summary>
public interface ISalesRepSearchService
{
    /// <summary>Runs the paged Sales Rep search and returns the matching page plus the total count.</summary>
    Task<SalesRepSearchResult> SearchAsync(SalesRepSearchCriteria criteria);
}
