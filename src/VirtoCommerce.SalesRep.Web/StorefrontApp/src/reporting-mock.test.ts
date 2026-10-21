import { describe, expect, it } from "vitest";
import { buildReportingMonths, toReportingMonth, withMonthChanges } from "./reporting";
import { buildReportingMock } from "./reporting-mock";

const NOW = new Date(2026, 9, 21, 15, 0, 0);

describe("buildReportingMock", () => {
  const months = buildReportingMonths("year", NOW);

  it("fills every month with data and last year, leaving upcoming months empty", () => {
    const { raw } = buildReportingMock(months, "");

    raw.forEach((month, i) => {
      if (months[i].state === "upcoming") {
        expect(month).toEqual({});
      } else {
        expect(month.all?.amount).toBeGreaterThan(0);
        expect(month.lastYear?.amount).toBeGreaterThanOrEqual(0);
        expect(month.cancelled?.amount).toBeLessThan(month.all?.amount ?? 0);
      }
    });
  });

  it("is stable for the same customer and differs between customers", () => {
    expect(buildReportingMock(months, "org-1")).toEqual(buildReportingMock(months, "org-1"));
    expect(buildReportingMock(months, "org-1").raw[0]).not.toEqual(buildReportingMock(months, "org-2").raw[0]);
  });

  it("produces both rises and falls vs last year, so both badge colours show", () => {
    const { raw, before } = buildReportingMock(months, "");
    const changes = withMonthChanges(
      months.map((month, i) => toReportingMonth(month, raw[i])),
      before,
    ).flatMap((month) => (month.revenueChange === null ? [] : [month.revenueChange]));

    expect(changes.some((change) => change > 0)).toBe(true);
    expect(changes.some((change) => change < 0)).toBe(true);
  });
});

describe("buildReportingMock: the empty month last year", () => {
  it("has no sales last March for every customer, so that month has no YoY change", () => {
    const months = buildReportingMonths("year", NOW);
    const march = months.findIndex((month) => month.month === 2);

    for (const customer of ["", "org-1", "org-2"]) {
      const { raw } = buildReportingMock(months, customer);

      expect(raw[march].lastYear).toEqual({ amount: 0, count: 0 });
      expect(toReportingMonth(months[march], raw[march]).revenueChange).toBeNull();
      expect(raw[march].all?.amount).toBeGreaterThan(0);
    }
  });
});
