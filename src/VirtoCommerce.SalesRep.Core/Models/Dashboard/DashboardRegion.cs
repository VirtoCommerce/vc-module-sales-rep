using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models.Dashboard;

public class DashboardRegion
{
    public string Id { get; set; }

    public IList<DashboardBlock> Blocks { get; set; } = [];
}
