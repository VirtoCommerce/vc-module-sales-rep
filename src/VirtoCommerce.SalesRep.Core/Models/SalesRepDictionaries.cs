using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDictionaries
{
    public IList<SalesRepCountry> Countries { get; set; } = [];
    public IList<SalesRepCurrency> Currencies { get; set; } = [];
    public IList<string> Languages { get; set; } = [];
}

public class SalesRepCountry
{
    public string Id { get; set; }
    public string Name { get; set; }
}

public class SalesRepCurrency
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Symbol { get; set; }
}
