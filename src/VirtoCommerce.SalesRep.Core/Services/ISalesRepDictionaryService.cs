using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>Supplies the reference data (countries, currencies, languages) the Sales Rep admin UI dropdowns need.</summary>
public interface ISalesRepDictionaryService
{
    Task<SalesRepDictionaries> GetDictionariesAsync();
}
