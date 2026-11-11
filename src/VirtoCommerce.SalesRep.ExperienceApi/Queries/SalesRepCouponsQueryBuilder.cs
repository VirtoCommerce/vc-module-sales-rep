using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCouponsQueryBuilder : SalesRepSearchQueryBuilder<SalesRepCouponsQuery, SalesRepCouponSearchResult, SalesRepCoupon, SalesRepCouponType>
{
    protected override string Name => "salesRepCoupons";

    public SalesRepCouponsQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
