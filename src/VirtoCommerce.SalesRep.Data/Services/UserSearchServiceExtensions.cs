using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class UserSearchServiceExtensions
{
    public static async Task<IList<ApplicationUser>> SearchAllAsync(this IUserSearchService userSearchService, UserSearchCriteria criteria, int pageSize = 50)
    {
        List<ApplicationUser> result = [];
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
