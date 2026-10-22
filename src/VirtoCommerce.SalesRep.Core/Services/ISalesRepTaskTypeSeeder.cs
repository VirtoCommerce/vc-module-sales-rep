using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepTaskTypeSeeder
{
    Task EnsureCallTypeAsync();
}
