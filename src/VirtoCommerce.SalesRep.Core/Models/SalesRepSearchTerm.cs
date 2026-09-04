using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepSearchTerm
{
    public string Term { get; set; }

    public int Count { get; set; }

    public DateTime? LastSearchedDate { get; set; }
}
