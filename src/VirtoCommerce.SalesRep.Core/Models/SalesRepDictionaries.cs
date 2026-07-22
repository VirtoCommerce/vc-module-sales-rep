using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Reference/lookup data the Sales Rep admin blade needs to populate its Profile/Address dropdowns, sourced from
/// VirtoCommerce data (platform countries, Core currencies, the configured languages setting) — the same sources
/// the customer contact admin uses — rather than a hard-coded/browser list.
/// </summary>
/// <remarks>
/// This is a module-local aggregation endpoint because the underlying catalogs are not otherwise reachable from a
/// generated client: the platform countries controller is hidden from Swagger (<c>ApiExplorerSettings(IgnoreApi)</c>)
/// and Core currencies live in a module the app does not generate a client for. Once those are exposed upstream this
/// endpoint (and <see cref="Services.ISalesRepDictionaryService"/>) can be removed in favour of the platform/Core clients.
/// </remarks>
public class SalesRepDictionaries
{
    public IList<SalesRepCountry> Countries { get; set; } = [];
    public IList<SalesRepCurrency> Currencies { get; set; } = [];
    public IList<string> Languages { get; set; } = [];
}
