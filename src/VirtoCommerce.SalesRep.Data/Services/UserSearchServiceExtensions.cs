using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class UserSearchServiceExtensions
{
    /// <summary>
    /// Enumerates ALL users matching the criteria by paging internally, so no single unbounded page is fetched.
    /// IUserSearchService doesn't implement ISearchService (so the platform SearchAllAsync extension doesn't
    /// apply), but UserSearchCriteria/UserSearchResult already support paging and SearchUsersAsync already
    /// pages — this is the same loop the platform's SearchAllAsync runs over SearchBatchesAsync.
    /// </summary>
    public static async Task<IList<ApplicationUser>> SearchAllAsync(this IUserSearchService userSearchService, UserSearchCriteria criteria, int pageSize = 50)
    {
        var result = new List<ApplicationUser>();
        criteria.Skip = 0;
        criteria.Take = pageSize;

        int totalCount;
        do
        {
            var page = await userSearchService.SearchUsersAsync(criteria);
            if (page.Results.Count == 0)
            {
                break;
            }

            result.AddRange(page.Results);
            totalCount = page.TotalCount;
            criteria.Skip += criteria.Take;
        }
        while (criteria.Skip < totalCount);

        return result;
    }
}
