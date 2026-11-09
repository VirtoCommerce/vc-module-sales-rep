using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCouponType : ExtendableGraphType<SalesRepCoupon>
{
    public SalesRepCouponType()
    {
        Name = "SalesRepCoupon";

        Field(x => x.Id, nullable: false).Description("Coupon id.");
        Field(x => x.CouponCode, nullable: false).Description("The code a buyer enters at checkout.");
        Field(x => x.PromotionId, nullable: false).Description("Id of the promotion the coupon belongs to.");
        Field(x => x.SystemName, nullable: true).Description("Promotion name as set up in the back office.");
        Field(x => x.Name, nullable: true).Description("Localized promotion display name, falling back to its name.");
        Field(x => x.Label, nullable: true).Description("Localized promotion label, e.g. \"15% OFF\".");
        Field(x => x.Description, nullable: true).Description("Localized promotion description, falling back to its description.");
        Field(x => x.StartDate, nullable: true).Description("When the promotion starts.");
        Field(x => x.EndDate, nullable: true).Description("When the coupon stops working: the earlier of its own expiration and the promotion's end.");
        Field(x => x.PromotionEndDate, nullable: true).Description("When the promotion ends.");
        Field(x => x.ExpirationDate, nullable: true).Description("The coupon's own expiration, if it has one.");
        Field(x => x.MaxUsesNumber, nullable: false).Description("Total uses allowed across all buyers; 0 means unlimited.");
        Field(x => x.MaxUsesPerUser, nullable: false).Description("Uses allowed per buyer; 0 means unlimited.");
        Field(x => x.TotalUsesCount, nullable: false).Description("Times the coupon has been used so far.");
    }
}
