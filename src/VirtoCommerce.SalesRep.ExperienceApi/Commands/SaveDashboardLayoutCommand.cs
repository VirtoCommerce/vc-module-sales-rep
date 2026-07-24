using System.Collections.Generic;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SaveDashboardLayoutCommand : ICommand<DashboardLayout>
{
    public string Scope { get; set; }

    public string StoreId { get; set; }

    public int SchemaVersion { get; set; }

    public IList<DashboardRegion> Regions { get; set; } = [];

    public string UserId { get; set; }
}
