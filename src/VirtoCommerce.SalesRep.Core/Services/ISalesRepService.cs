using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepService : ICrudService<SalesRepDetails>
{
    Task BlockAsync(string id);

    Task UnblockAsync(string id);

    Task SetPasswordAsync(string id, string newPassword);

    Task<IList<SalesRepRole>> GetRolesAsync();
}
