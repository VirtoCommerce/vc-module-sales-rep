import type { SalesRepCouponType } from "./composables/useSalesRepCoupons";
import type { ComposerTranslation } from "vue-i18n";

// The line under a coupon's description, on both the profile widget and the all-coupons page.
export function couponUsesLabel(
  coupon: Pick<SalesRepCouponType, "maxUsesNumber" | "totalUsesCount">,
  t: ComposerTranslation,
): string {
  // MaxUsesNumber 0 means no cap in vc-module-marketing.
  if (!coupon.maxUsesNumber) {
    return t("sales_rep.coupons.unlimited");
  }

  const left = Math.max(coupon.maxUsesNumber - coupon.totalUsesCount, 0);

  // Plural on `left`: it is what the noun agrees with ("1 of 5 use left", "2 of 5 uses left").
  return t("sales_rep.coupons.uses_left", { left, total: coupon.maxUsesNumber }, left);
}
