using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCustomerSortRuleResolver : ISortRuleResolver<SalesRepCustomerSortRule>
{
    Task<SalesRepCustomerSortSpec> ResolveSortAsync(string storeId, string sort);
}
