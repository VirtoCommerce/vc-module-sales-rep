using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepRecipientResolver
{
    Task<IList<Member>> ResolveRecipientsAsync(string organizationId, string responseGroup);
}
