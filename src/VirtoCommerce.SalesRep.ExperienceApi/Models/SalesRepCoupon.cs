using System;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

// A promotion coupon as the rep's storefront shows it. The promotion half mirrors xAPI PromotionCouponType field for
// field (id aside), so the storefront hands an item straight to the host's CouponItem card.
public class SalesRepCoupon
{
    public string Id { get; set; }

    public string CouponCode { get; set; }

    public string PromotionId { get; set; }

    public string SystemName { get; set; }

    public string Name { get; set; }

    public string Label { get; set; }

    public string Description { get; set; }

    public DateTime? StartDate { get; set; }

    // What the card shows as "Expires": see GetEndDate.
    public DateTime? EndDate { get; set; }

    public DateTime? PromotionEndDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // 0 = no cap, as in vc-module-marketing.
    public int MaxUsesNumber { get; set; }

    public int MaxUsesPerUser { get; set; }

    public int TotalUsesCount { get; set; }

    public static SalesRepCoupon FromCoupon(Coupon coupon, Promotion promotion, string cultureName)
    {
        var result = AbstractTypeFactory<SalesRepCoupon>.TryCreateInstance();
        result.MapFrom(coupon, promotion, cultureName);
        return result;
    }

    protected virtual void MapFrom(Coupon coupon, Promotion promotion, string cultureName)
    {
        Id = coupon.Id;
        CouponCode = coupon.Code;
        PromotionId = coupon.PromotionId;
        SystemName = promotion.Name;
        Name = Localize(promotion.LocalizedDisplayName, cultureName, promotion.Name);
        Label = Localize(promotion.LocalizedLabel, cultureName);
        Description = Localize(promotion.LocalizedDescription, cultureName, promotion.Description);
        StartDate = promotion.StartDate;
        EndDate = GetEndDate(coupon, promotion);
        PromotionEndDate = promotion.EndDate;
        ExpirationDate = coupon.ExpirationDate;
        MaxUsesNumber = coupon.MaxUsesNumber;
        MaxUsesPerUser = coupon.MaxUsesPerUser;
        TotalUsesCount = (int)Math.Min(coupon.TotalUsesCount, int.MaxValue);
    }

    // When the coupon stops working: the earlier of its own expiration and the promotion's end, since checkout
    // evaluates only active promotions whatever the coupon's expiration.
    public static DateTime? GetEndDate(Coupon coupon, Promotion promotion)
    {
        if (coupon.ExpirationDate == null || promotion.EndDate == null)
        {
            return coupon.ExpirationDate ?? promotion.EndDate;
        }

        return coupon.ExpirationDate < promotion.EndDate ? coupon.ExpirationDate : promotion.EndDate;
    }

    // Same fallback as xAPI promotionCoupons: the culture's value, else the invariant field.
    protected static string Localize(LocalizedString value, string cultureName, string fallback = null)
    {
        var localized = string.IsNullOrEmpty(cultureName) ? null : value?.GetValue(cultureName);

        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }
}
