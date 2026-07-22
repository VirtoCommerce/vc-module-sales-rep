using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDictionaryService
{
    Task<SalesRepDictionaries> GetDictionariesAsync();
}
