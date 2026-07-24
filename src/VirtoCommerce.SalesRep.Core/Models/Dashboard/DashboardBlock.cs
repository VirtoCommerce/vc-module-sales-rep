using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models.Dashboard;

public class DashboardBlock
{
    public string Id { get; set; }

    public string Type { get; set; }

    public bool Hidden { get; set; }

    public IList<DashboardSetting> Settings { get; set; } = [];
}
