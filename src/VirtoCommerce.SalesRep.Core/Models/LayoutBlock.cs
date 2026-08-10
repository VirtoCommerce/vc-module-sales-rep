using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models;

public class LayoutBlock
{
    public string Id { get; set; }

    public string Type { get; set; }

    public bool Hidden { get; set; }

    public IList<LayoutSetting> Settings { get; set; } = [];
}
