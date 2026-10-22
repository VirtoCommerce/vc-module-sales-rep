import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import { couponUsesLabel } from "./coupons";
import enMessages from "./locales/en.json";
import ptMessages from "./locales/pt.json";

// Real messages through vue-i18n's own plural rule: the host's CLDR rule defers to it for messages with fewer than
// four forms, which is every language below.
function translate(locale: "en" | "pt") {
  // Keyed by any locale, as the app's own messages are: literal keys would narrow `t` to these two locales.
  const messages: Record<string, typeof enMessages> = { en: enMessages, pt: ptMessages };
  return createI18n({ legacy: false, locale, messages }).global.t;
}

describe("couponUsesLabel", () => {
  it.each([
    [1, 0, "1 use left out of 1"],
    [5, 4, "1 use left out of 5"],
    [5, 3, "2 uses left out of 5"],
    [1, 1, "0 uses left out of 1"],
  ])("agrees with the uses left (%i max, %i used)", (maxUsesNumber, totalUsesCount, expected) => {
    expect(couponUsesLabel({ maxUsesNumber, totalUsesCount }, translate("en"))).toBe(expected);
  });

  it("takes the singular in a three-form language too", () => {
    expect(couponUsesLabel({ maxUsesNumber: 3, totalUsesCount: 2 }, translate("pt"))).toBe("Resta 1 uso de 3");
    expect(couponUsesLabel({ maxUsesNumber: 3, totalUsesCount: 1 }, translate("pt"))).toBe("Restam 2 usos de 3");
  });

  it("reads unlimited when the coupon has no cap", () => {
    expect(couponUsesLabel({ maxUsesNumber: 0, totalUsesCount: 7 }, translate("en"))).toBe("Unlimited");
  });
});
