import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import { couponUsesLabel } from "./coupons";
import enMessages from "./locales/en.json";
import ptMessages from "./locales/pt.json";

// Real messages through vue-i18n's own plural rule: the host's CLDR rule defers to it for messages with fewer than
// four forms, which is every language below.
function translate(locale: "en" | "pt") {
  const i18n = createI18n({ legacy: false, locale, messages: { en: enMessages, pt: ptMessages } });
  return i18n.global.t as (key: string, params?: Record<string, unknown>, plural?: number) => string;
}

describe("couponUsesLabel", () => {
  it.each([
    [1, 0, "1 of 1 use left"],
    [5, 4, "1 of 5 use left"],
    [5, 3, "2 of 5 uses left"],
    [1, 1, "0 of 1 uses left"],
  ])("agrees with the uses left (%i max, %i used)", (maxUsesNumber, totalUsesCount, expected) => {
    expect(couponUsesLabel({ maxUsesNumber, totalUsesCount }, translate("en"))).toBe(expected);
  });

  it("takes the singular in a three-form language too", () => {
    expect(couponUsesLabel({ maxUsesNumber: 3, totalUsesCount: 2 }, translate("pt"))).toBe("Resta 1 de 3 uso");
    expect(couponUsesLabel({ maxUsesNumber: 3, totalUsesCount: 1 }, translate("pt"))).toBe("Restam 2 de 3 usos");
  });

  it("reads unlimited when the coupon has no cap", () => {
    expect(couponUsesLabel({ maxUsesNumber: 0, totalUsesCount: 7 }, translate("en"))).toBe("Unlimited");
  });
});
