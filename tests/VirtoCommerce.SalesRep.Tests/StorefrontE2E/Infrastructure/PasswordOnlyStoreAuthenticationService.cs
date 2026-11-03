using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>The storefront's sign-in form needs the password scheme listed as active.</summary>
internal sealed class PasswordOnlyStoreAuthenticationService : IStoreAuthenticationService
{
    public Task<IList<StoreAuthenticationScheme>> GetStoreSchemesAsync(string storeId, bool clone = true)
        => Task.FromResult<IList<StoreAuthenticationScheme>>([new StoreAuthenticationScheme { Name = "Password", IsActive = true }]);

    public Task SaveStoreSchemesAsync(string storeId, IList<StoreAuthenticationScheme> models) => Task.CompletedTask;
}
