using System.Collections.Generic;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SaveLayoutCommand : ICommand<Layout>
{
    public string Scope { get; set; }

    public string StoreId { get; set; }

    public int SchemaVersion { get; set; }

    public IList<LayoutRegion> Regions { get; set; } = [];

    public string UserId { get; set; }
}
