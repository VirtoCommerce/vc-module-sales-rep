using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepOrderStatusService
{
    Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName);

    Task<IList<string>> ResolveOrderStatusesAsync(string storeId, IList<string> selectedStatusNames);
}
