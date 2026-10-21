import { describe, expect, it } from "vitest";
import {
  buildReportingMonths,
  niceTicks,
  percentChange,
  smoothLinePath,
  previousMonthWindow,
  summarizeReporting,
  toReportingMonth,
  withMonthChanges,
  REPORTING_PERIODS,
} from "./reporting";
import type { ReportingMonthSlotType } from "./reporting";

// Local-time constructors, as in utils.test.ts, so the windows read the same in any time zone.
const localIso = (year: number, month: number, day: number, h = 0, min = 0, s = 0, ms = 0): string =>
  new Date(year, month, day, h, min, s, ms).toISOString();

const NOW = new Date(2026, 9, 21, 15, 0, 0); // Oct 21, 2026

function slot(overrides: Partial<ReportingMonthSlotType> = {}): ReportingMonthSlotType {
  return {
    year: 2026,
    month: 8,
    state: "closed",
    from: "",
    to: "",
    lyFrom: "",
    lyTo: "",
    daysPassed: 30,
    daysInMonth: 30,
    ...overrides,
  };
}

describe("buildReportingMonths", () => {
  it("covers January to December of the current year, with the months after today upcoming", () => {
    const months = buildReportingMonths("year", NOW);

    expect(months).toHaveLength(12);
    expect(months.map((month) => month.state)).toEqual([
      ...Array<string>(9).fill("closed"),
      "current",
      "upcoming",
      "upcoming",
    ]);
  });

  it("rolls back across the year boundary for the last 12 months", () => {
    const months = buildReportingMonths("12m", NOW);

    expect(months[0]).toMatchObject({ year: 2025, month: 10 });
    expect(months.at(-1)).toMatchObject({ year: 2026, month: 9, state: "current" });
  });

  it("returns the three months of a quarter", () => {
    expect(buildReportingMonths("q2", NOW).map((month) => month.month)).toEqual([3, 4, 5]);
    expect(buildReportingMonths("q4", NOW).map((month) => month.state)).toEqual(["current", "upcoming", "upcoming"]);
  });

  it("ends the current month at the end of today and compares it with the whole month last year", () => {
    const current = buildReportingMonths("6m", NOW).at(-1);

    expect(current).toMatchObject({
      from: localIso(2026, 9, 1),
      to: localIso(2026, 9, 21, 23, 59, 59, 999),
      lyFrom: localIso(2025, 9, 1),
      lyTo: localIso(2025, 9, 31, 23, 59, 59, 999),
      daysPassed: 21,
      daysInMonth: 31,
    });
  });

  it("ends a closed month on its last day, leap February included", () => {
    const february = buildReportingMonths("q1", new Date(2028, 5, 1))[1];

    expect(february.to).toBe(localIso(2028, 1, 29, 23, 59, 59, 999));
    expect(february.lyTo).toBe(localIso(2027, 1, 28, 23, 59, 59, 999));
  });

  it("never exceeds the query's month slots", () => {
    for (const period of REPORTING_PERIODS) {
      expect(buildReportingMonths(period, NOW).length).toBeLessThanOrEqual(12);
    }
  });
});

describe("toReportingMonth", () => {
  it("takes cancelled orders out of revenue, orders and AOV", () => {
    const month = toReportingMonth(slot(), {
      all: { amount: 1200, count: 4 },
      cancelled: { amount: 200, count: 1 },
      lastYear: { amount: 800, count: 2 },
      lastYearCancelled: { amount: 0, count: 0 },
    });

    expect(month).toMatchObject({ revenue: 1000, orders: 3, lastYearRevenue: 800, lastYearAov: 400 });
    expect(month.aov).toBeCloseTo(333.33, 2);
    expect(month.revenueChange).toBe(25);
  });

  it("leaves AOV and the change empty instead of dividing by zero", () => {
    const month = toReportingMonth(slot(), {
      all: { amount: 300, count: 1 },
      cancelled: { amount: 300, count: 1 },
      lastYear: { amount: 0, count: 0 },
    });

    expect(month).toMatchObject({ revenue: 0, orders: 0, aov: null, revenueChange: null, aovChange: null });
  });

  it("never goes negative when the cancelled slice outgrows the total", () => {
    const month = toReportingMonth(slot(), { all: { amount: 100, count: 1 }, cancelled: { amount: 150, count: 2 } });

    expect(month).toMatchObject({ revenue: 0, orders: 0 });
  });

  it("projects the current month to its end and compares the projection with last year", () => {
    const month = toReportingMonth(slot({ state: "current", daysPassed: 10, daysInMonth: 30 }), {
      all: { amount: 1000, count: 5 },
      lastYear: { amount: 2000, count: 10 },
    });

    expect(month).toMatchObject({ projectedRevenue: 3000, revenueChange: 50 });
  });

  it("reports nothing for an upcoming month", () => {
    const month = toReportingMonth(slot({ state: "upcoming" }), { all: { amount: 10, count: 1 } });

    expect(month).toMatchObject({ revenue: null, orders: null, aov: null, projectedRevenue: null });
  });
});

describe("summarizeReporting", () => {
  it("averages closed months only and divides period revenue by period orders", () => {
    const months = [
      toReportingMonth(slot(), { all: { amount: 1000, count: 2 } }),
      toReportingMonth(slot(), { all: { amount: 3000, count: 2 } }),
      toReportingMonth(slot({ state: "current", daysPassed: 1, daysInMonth: 30 }), { all: { amount: 600, count: 1 } }),
      toReportingMonth(slot({ state: "upcoming" }), {}),
    ];

    expect(summarizeReporting(months)).toMatchObject({
      averageRevenuePerClosedMonth: 2000,
      periodAov: 920,
      revenue: 4600,
      orders: 5,
    });
  });

  it("returns empty figures without closed months or orders", () => {
    expect(summarizeReporting([toReportingMonth(slot({ state: "upcoming" }), {})])).toMatchObject({
      averageRevenuePerClosedMonth: null,
      periodAov: null,
      revenueChange: null,
      currentMonth: null,
    });
  });
});

describe("percentChange", () => {
  it("returns null without a positive baseline", () => {
    expect(percentChange(10, 0)).toBeNull();
    expect(percentChange(null, 10)).toBeNull();
    expect(percentChange(90, 100)).toBe(-10);
  });
});

describe("niceTicks", () => {
  it("rounds the axis up to a readable step", () => {
    expect(niceTicks(9_400_000)).toEqual([0, 2_500_000, 5_000_000, 7_500_000, 10_000_000]);
    expect(niceTicks(362)).toEqual([0, 100, 200, 300, 400]);
  });

  it("falls back to unit steps for an empty chart", () => {
    expect(niceTicks(0)).toEqual([0, 1, 2, 3, 4]);
    expect(niceTicks(Number.NaN)).toEqual([0, 1, 2, 3, 4]);
  });
});

describe("previousMonthWindow", () => {
  it("returns the month before January for the current year", () => {
    expect(previousMonthWindow("year", NOW)).toEqual({
      from: localIso(2025, 11, 1),
      to: localIso(2025, 11, 31, 23, 59, 59, 999),
    });
  });

  it("returns the month before a rolling period", () => {
    expect(previousMonthWindow("6m", NOW).from).toBe(localIso(2026, 3, 1));
  });
});

describe("withMonthChanges", () => {
  it("compares each month with the one before, the first with the month before the period", () => {
    const months = withMonthChanges(
      [
        toReportingMonth(slot(), { all: { amount: 1200, count: 4 } }),
        toReportingMonth(slot(), { all: { amount: 900, count: 2 } }),
      ],
      { all: { amount: 1100, count: 4 }, cancelled: { amount: 100, count: 0 } },
    );

    expect(months.map((month) => month.revenueMonthChange)).toEqual([20, -25]);
    expect(months.map((month) => month.aovMonthChange)).toEqual([20, 50]);
  });

  it("compares the current month's projection and stops at upcoming months", () => {
    const months = withMonthChanges([
      toReportingMonth(slot(), { all: { amount: 1000, count: 2 } }),
      toReportingMonth(slot({ state: "current", daysPassed: 15, daysInMonth: 30 }), { all: { amount: 600, count: 1 } }),
      toReportingMonth(slot({ state: "upcoming" }), {}),
    ]);

    expect(months.map((month) => month.revenueMonthChange)).toEqual([null, 20, null]);
  });
});

describe("summarizeReporting vs last year", () => {
  it("prorates the current month of last year to the same day before comparing", () => {
    const summary = summarizeReporting([
      toReportingMonth(slot(), { all: { amount: 1100, count: 2 }, lastYear: { amount: 1000, count: 2 } }),
      toReportingMonth(slot({ state: "current", daysPassed: 15, daysInMonth: 30 }), {
        all: { amount: 600, count: 2 },
        lastYear: { amount: 1000, count: 4 },
      }),
    ]);

    // Last year: 1000 + 1000 × 15/30 = 1500 revenue, 2 + 4 × 15/30 = 4 orders.
    expect(summary.revenueChange).toBeCloseTo(13.33, 2);
    expect(summary.ordersChange).toBe(0);
    expect(summary.aovChange).toBeCloseTo(13.33, 2);
    expect(summary.currentMonth).toMatchObject({ revenue: 600, projectedRevenue: 1200, daysPassed: 15 });
  });

  it("has no change without any last-year data", () => {
    const summary = summarizeReporting([toReportingMonth(slot(), { all: { amount: 100, count: 1 } })]);

    expect(summary).toMatchObject({ revenueChange: null, ordersChange: null, aovChange: null });
  });
});

describe("smoothLinePath", () => {
  it("passes through every point and flattens the tangent at a peak", () => {
    const path = smoothLinePath([
      { x: 0, y: 100 },
      { x: 30, y: 20 },
      { x: 60, y: 100 },
    ]);

    expect(path.startsWith("M0,100 C")).toBe(true);
    expect(path).toContain(" 30,20 ");
    expect(path.endsWith(" 60,100")).toBe(true);
    // At the peak both handles share its y, so the curve is rounded rather than pointed.
    expect(path).toContain("20,20 30,20 C40,20");
  });

  it("does not overshoot a monotone run", () => {
    const path = smoothLinePath([
      { x: 0, y: 0 },
      { x: 10, y: 0 },
      { x: 20, y: 50 },
    ]);
    const controlYs = [...path.matchAll(/(-?[\d.]+),(-?[\d.]+)/g)].map((match) => Number(match[2]));

    expect(Math.min(...controlYs)).toBeGreaterThanOrEqual(0);
    expect(Math.max(...controlYs)).toBeLessThanOrEqual(50);
  });

  it("handles one point and no points", () => {
    expect(smoothLinePath([])).toBe("");
    expect(smoothLinePath([{ x: 5, y: 7 }])).toBe("M5,7");
  });
});
