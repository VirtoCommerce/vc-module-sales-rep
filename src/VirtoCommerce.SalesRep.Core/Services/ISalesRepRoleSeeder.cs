using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepRoleSeeder
{
    Task EnsureDocumentRolesAsync();
}
