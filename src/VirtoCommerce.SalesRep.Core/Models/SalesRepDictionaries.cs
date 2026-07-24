using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDictionaries
{
    public IList<SalesRepCountry> Countries { get; set; } = [];
    public IList<SalesRepCurrency> Currencies { get; set; } = [];
    public IList<string> Languages { get; set; } = [];
}
