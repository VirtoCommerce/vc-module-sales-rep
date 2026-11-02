using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>No store is resolved by domain; the resolver then falls back to StoresOptions.DefaultStore.</summary>
internal sealed class EmptyStoreSearchService : IStoreSearchService
{
    public Task<StoreSearchResult> SearchAsync(StoreSearchCriteria criteria, bool clone = true)
        => Task.FromResult(AbstractTypeFactory<StoreSearchResult>.TryCreateInstance());
}
