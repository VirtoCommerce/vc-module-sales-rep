using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using CartAggregate = VirtoCommerce.XCart.Core.CartAggregate;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>The cart type's constructor dependency; the storefront's short cart never resolves these fields here.</summary>
internal sealed class NoCartAvailMethodsService : ICartAvailMethodsService
{
    public Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(CartAggregate cartAggregate) => Task.FromResult<IEnumerable<PaymentMethod>>([]);
    public Task<IEnumerable<ShippingRate>> GetAvailableShippingRatesAsync(CartAggregate cartAggregate) => Task.FromResult<IEnumerable<ShippingRate>>([]);
    public Task<IEnumerable<GiftItem>> GetAvailableGiftsAsync(CartAggregate cartAggregate) => Task.FromResult<IEnumerable<GiftItem>>([]);
}
