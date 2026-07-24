using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepPrimaryContactResolver
{
    Task<Contact> ResolvePrimaryContactAsync(Organization organization, string responseGroup);
}
