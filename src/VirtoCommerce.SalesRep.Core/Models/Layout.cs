using System;
using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models;

public class Layout
{
    public int SchemaVersion { get; set; }

    public IList<LayoutRegion> Regions { get; set; } = [];

    public DateTime? ModifiedDate { get; set; }
}
