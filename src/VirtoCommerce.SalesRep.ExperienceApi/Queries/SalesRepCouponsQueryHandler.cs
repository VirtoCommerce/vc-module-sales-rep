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
    private readonly IOptionalDependency<IPromotionUsageSearchService> _promotionUsageSearchService;
    private readonly ISalesRepStoreAccessService _storeAccessService;

    public SalesRepCouponsQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IPromotionSearchService> promotionSearchService,
        IOptionalDependency<ICouponSearchService> couponSearchService,
        IOptionalDependency<IPromotionUsageSearchService> promotionUsageSearchService,
        ISalesRepStoreAccessService storeAccessService)
        : base(organizationAccessService)
    {
        _promotionSearchService = promotionSearchService;
        _couponSearchService = couponSearchService;
        _promotionUsageSearchService = promotionUsageSearchService;
        _storeAccessService = storeAccessService;
    }

    public virtual async Task<SalesRepCouponSearchResult> Handle(SalesRepCouponsQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepCouponSearchResult>.TryCreateInstance();

        // Optional dependency: with vc-module-marketing absent every read is simply empty.
        if (!_promotionSearchService.HasValue || !_couponSearchService.HasValue || !_promotionUsageSearchService.HasValue ||
            !await IsSalesRepAsync(request.UserId))
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

        // Cloned: the uses count is overwritten below, and the cached coupons must stay as they are.
        var coupons = await _couponSearchService.Value.SearchAllAsync(BuildCouponSearchCriteria(promotions));
        await RefreshTotalUsesCountAsync(coupons);

        var promotionsById = promotions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
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

    // The same promotions as xAPI promotionCoupons; expired, used-up and member-only coupons are dropped below.
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

    // A coupon's TotalUsesCount is computed when it loads and then cached, and recording a use doesn't evict it.
    // Recount capped coupons the way checkout does (DynamicPromotion.GetUsageCount): the usage search's cache is
    // evicted on every usage write.
    protected virtual async Task RefreshTotalUsesCountAsync(IList<Coupon> coupons)
    {
        foreach (var coupon in coupons.Where(x => x.MaxUsesNumber > 0))
        {
            var criteria = AbstractTypeFactory<PromotionUsageSearchCriteria>.TryCreateInstance();
            criteria.PromotionId = coupon.PromotionId;
            criteria.CouponCode = coupon.Code;
            criteria.Take = 0;

            coupon.TotalUsesCount = (await _promotionUsageSearchService.Value.SearchAsync(criteria)).TotalCount;
        }
    }

    protected virtual IList<Coupon> ToAvailableCoupons(IList<Coupon> coupons, IDictionary<string, Promotion> promotionsById)
    {
        var now = DateTime.UtcNow;

        return coupons
            .Where(x => x.ExpirationDate == null || x.ExpirationDate > now)
            .Where(x => x.MaxUsesNumber == 0 || x.TotalUsesCount < x.MaxUsesNumber)
            // Checkout accepts a coupon registered to a member only from that contact or organization, and this list
            // isn't scoped to one customer.
            .Where(x => string.IsNullOrWhiteSpace(x.MemberId))
            // Soonest to expire first, like the storefront's own coupon list; the id keeps pages stable.
            .OrderBy(x => SalesRepCoupon.GetEndDate(x, promotionsById[x.PromotionId]) ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .ToList();
    }
}
