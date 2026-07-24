using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface IDashboardLayoutService
{
    Task<DashboardLayout> GetLayoutAsync(string userId, string scope, string storeId = null);

    Task SaveLayoutAsync(string userId, string scope, DashboardLayout layout, string storeId = null);
}
