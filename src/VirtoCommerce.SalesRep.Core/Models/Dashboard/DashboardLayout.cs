using System;
using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models.Dashboard;

public class DashboardLayout
{
    public int SchemaVersion { get; set; }

    public IList<DashboardRegion> Regions { get; set; } = [];

    public DateTime? ModifiedDate { get; set; }
}
