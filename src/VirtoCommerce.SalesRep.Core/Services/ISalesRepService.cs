using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepService
{
    Task<SalesRepDetails> GetByIdAsync(string id);

    Task<SalesRepDetails> SaveChangesAsync(SalesRepDetails salesRep);

    Task DeleteAsync(string[] ids);

    Task BlockAsync(string id);

    Task UnblockAsync(string id);

    Task SetPasswordAsync(string id, string newPassword);

    Task<IList<SalesRepRole>> GetRolesAsync();
}
