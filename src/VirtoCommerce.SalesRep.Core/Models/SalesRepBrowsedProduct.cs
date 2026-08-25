using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepBrowsedProduct
{
    public string Code { get; set; }

    public string ProductId { get; set; }

    public string Name { get; set; }

    public string Slug { get; set; }

    public string ImageUrl { get; set; }

    public int ViewCount { get; set; }

    public DateTime? LastViewedDate { get; set; }
}
