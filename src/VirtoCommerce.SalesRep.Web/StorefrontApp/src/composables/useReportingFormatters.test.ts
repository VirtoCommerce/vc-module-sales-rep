import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { defineComponent } from "vue";
import { createI18n } from "vue-i18n";
import { useReportingFormatters } from "./useReportingFormatters";

function formatters(): ReturnType<typeof useReportingFormatters> {
  let result: ReturnType<typeof useReportingFormatters> | undefined;
  const Host = defineComponent({
    setup() {
      result = useReportingFormatters();
      return () => null;
    },
  });
  mount(Host, { global: { plugins: [createI18n({ legacy: false, locale: "en-US" })] } });
  return result!;
}

describe("useReportingFormatters", () => {
  it("prints every percent with one decimal", () => {
    const { share, change, signedChange } = formatters();

    expect(share(0.71)).toBe("71.0%");
    expect(change(5.5)).toBe("↑ 5.5%");
    expect(change(-4.44)).toBe("↓ 4.4%");
    expect(signedChange(23)).toBe("+23.0%");
  });

  it("prints a change that rounds to zero without an arrow or a sign", () => {
    const { change, signedChange } = formatters();

    expect(change(0.03)).toBe("0.0%");
    expect(change(-0.03)).toBe("0.0%");
    expect(signedChange(0)).toBe("0.0%");
  });

  it("lets a date that ends in an abbreviation dot share it with the sentence", () => {
    const { singleFullStop } = formatters();

    expect(singleFullStop("Okt. 2026 – Nov. 2026, laufender Monat bis 4. Nov.. Umsatz netto.")).toBe(
      "Okt. 2026 – Nov. 2026, laufender Monat bis 4. Nov. Umsatz netto.",
    );
    expect(singleFullStop("окт. 2026 г. – дек. 2026 г.. Продажи — нетто.")).toBe(
      "окт. 2026 г. – дек. 2026 г. Продажи — нетто.",
    );
    expect(singleFullStop("kuluva kuu 4.11. asti.")).toBe("kuluva kuu 4.11. asti.");
    expect(singleFullStop("Loading...")).toBe("Loading...");
  });

  it("prints a missing amount as a dash", () => {
    expect(formatters().money(null, "USD")).toBe("—");
  });
});
