using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models;

public class LayoutRegion
{
    public string Id { get; set; }

    public IList<LayoutBlock> Blocks { get; set; } = [];
}
