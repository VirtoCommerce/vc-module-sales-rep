using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.TaskManagement.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepTaskHandlerBase : SalesRepQueryHandlerBase
{
    protected SalesRepTaskHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
        : base(organizationAccessService)
    {
    }

    // Whose tasks the caller may see and change - the seam for widening beyond their own. Every read and every
    // mutation of an existing task answers through it. Creation does not: it stamps the caller directly, so it can
    // only ever produce a task they own. Empty means "nothing", never "everything".
    protected virtual Task<IList<string>> GetVisibleResponsibleIdsAsync(string userId, string memberId)
    {
        IList<string> result = string.IsNullOrEmpty(memberId) ? [] : [memberId];

        return Task.FromResult(result);
    }

    // A priority name in any case, for the writes and for the filter alike. Matched on the NAMES rather than
    // Enum.TryParse, which accepts two shapes that are not priority names: any numeric string, and a comma-separated
    // list - for every enum, not only a [Flags] one, ORing the members. So "3" and "Low, Normal" (1|2) would both have
    // read as High. Name matching rejects all three shapes at once.
    protected static bool TryParsePriority(string priority, out TaskPriority result)
    {
        var name = Enum.GetNames<TaskPriority>().FirstOrDefault(x => x.EqualsIgnoreCase(priority?.Trim()));

        result = name == null ? default : Enum.Parse<TaskPriority>(name);

        return name != null;
    }
}
