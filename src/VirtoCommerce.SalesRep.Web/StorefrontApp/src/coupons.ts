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

  // Plural on `left`: the messages keep the noun next to `left` ("1 use left out of 5", "2 uses left out of 5").
  return t("sales_rep.coupons.uses_left", { left, total: coupon.maxUsesNumber }, left);
}
