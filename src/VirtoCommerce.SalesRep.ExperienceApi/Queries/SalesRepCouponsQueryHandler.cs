using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Model.Promotions.Search;
using VirtoCommerce.MarketingModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCouponsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCouponsQuery, SalesRepCouponSearchResult>
{
    private readonly IOptionalDependency<IPromotionSearchService> _promotionSearchService;
    private readonly IOptionalDependency<ICouponSearchService> _couponSearchService;
    private readonly ISalesRepStoreAccessService _storeAccessService;

    public SalesRepCouponsQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IPromotionSearchService> promotionSearchService,
        IOptionalDependency<ICouponSearchService> couponSearchService,
        ISalesRepStoreAccessService storeAccessService)
        : base(organizationAccessService)
    {
        _promotionSearchService = promotionSearchService;
        _couponSearchService = couponSearchService;
        _storeAccessService = storeAccessService;
    }

    public virtual async Task<SalesRepCouponSearchResult> Handle(SalesRepCouponsQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepCouponSearchResult>.TryCreateInstance();

        // Optional dependency: with vc-module-marketing absent every read is simply empty.
        if (!_promotionSearchService.HasValue || !_couponSearchService.HasValue || !await IsSalesRepAsync(request.UserId))
        {
            return result;
        }

        // The store is a claim from the client. An empty one is refused outright: the promotion search reads it as
        // "every store", and the access service lets an unnamed store pass.
        if (string.IsNullOrWhiteSpace(request.StoreId) || !await _storeAccessService.IsAllowedAsync(request.UserId, request.StoreId))
        {
            return result;
        }

        // ponytail: pages in memory over ALL matching promotions, because CouponSearchCriteria cannot filter on expiry
        // or usage; fine for a store's tens of public coupons, needs those filters in vc-module-marketing at thousands.
        var promotions = await _promotionSearchService.Value.SearchAllNoCloneAsync(BuildPromotionSearchCriteria(request));
        if (promotions.Count == 0)
        {
            return result;
        }

        var coupons = await _couponSearchService.Value.SearchAllNoCloneAsync(BuildCouponSearchCriteria(promotions));

        var promotionsById = promotions.ToDictionary(x => x.Id);
        var available = ToAvailableCoupons(coupons, promotionsById);

        // Mapped after paging: only the returned page gets localized.
        result.TotalCount = available.Count;
        result.Results = available
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => SalesRepCoupon.FromCoupon(x, promotionsById[x.PromotionId], request.CultureName))
            .ToList();

        return result;
    }

    // The same set as xAPI promotionCoupons, so a rep sees what the customer sees on their Promotion coupons page.
    protected virtual PromotionSearchCriteria BuildPromotionSearchCriteria(SalesRepCouponsQuery request)
    {
        var criteria = AbstractTypeFactory<PromotionSearchCriteria>.TryCreateInstance();
        criteria.Store = request.StoreId;
        criteria.OnlyActive = true;
        criteria.IsPublic = true;
        // One shared code per promotion: a promotion with a generated batch of single-use codes is not listable.
        criteria.CouponCount = 1;

        return criteria;
    }

    protected virtual CouponSearchCriteria BuildCouponSearchCriteria(IList<Promotion> promotions)
    {
        var criteria = AbstractTypeFactory<CouponSearchCriteria>.TryCreateInstance();
        criteria.PromotionIds = promotions.Select(x => x.Id).ToList();

        return criteria;
    }

    protected virtual IList<Coupon> ToAvailableCoupons(IList<Coupon> coupons, IDictionary<string, Promotion> promotionsById)
    {
        var now = DateTime.UtcNow;

        return coupons
            .Where(x => x.ExpirationDate == null || x.ExpirationDate > now)
            .Where(x => x.MaxUsesNumber == 0 || x.TotalUsesCount < x.MaxUsesNumber)
            // Soonest to expire first, like the storefront's own coupon list (the card's end date: the coupon's
            // expiration, else the promotion's end); the id keeps pages stable.
            .OrderBy(x => x.ExpirationDate ?? promotionsById[x.PromotionId].EndDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .ToList();
    }
}
