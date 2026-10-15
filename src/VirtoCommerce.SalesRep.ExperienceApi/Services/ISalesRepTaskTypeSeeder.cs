using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepTaskTypeSeeder
{
    Task EnsureCallTypeAsync();
}
