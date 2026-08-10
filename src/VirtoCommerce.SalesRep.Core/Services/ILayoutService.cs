using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ILayoutService
{
    Task<Layout> GetLayoutAsync(string userId, string scope, string storeId = null);

    Task SaveLayoutAsync(string userId, string scope, Layout layout, string storeId = null);
}
