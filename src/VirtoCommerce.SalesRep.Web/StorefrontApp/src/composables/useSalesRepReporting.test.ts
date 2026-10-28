import { describe, expect, it, vi } from "vitest";
import { nextTick, ref } from "vue";
import { useSalesRepReporting } from "./useSalesRepReporting";
import type { SalesRepMonthlyOrderStatisticsQuery } from "../api/graphql/types";
import type { ReportingPeriodType } from "../reporting";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const result = ref<SalesRepMonthlyOrderStatisticsQuery | undefined>(undefined);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  const useQuery = vi.fn(() => ({ result, loading, error, onError: vi.fn() }));
  return { result, loading, error, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { storeId: "test-store", cultureName: "en-US", currencyCode: "USD" },
}));
// Oct 21, 2026.
vi.mock("./useToday", async () => {
  const { ref } = await import("vue");
  return { useToday: () => ref(new Date(2026, 9, 21).getTime()) };
});

type StatisticsType = NonNullable<SalesRepMonthlyOrderStatisticsQuery["salesRepCustomerOrderStatistics"]>;

function bucket(amount: number, count = 1) {
  return { count, total: { amount } };
}

function respond(buckets: Partial<Record<string, ReturnType<typeof bucket>>>): void {
  queryMock.result.value = {
    salesRepCustomerOrderStatistics: { currencyCode: "EUR", prev: bucket(0, 0), ...buckets } as StatisticsType,
  };
}

/** The reactive `variables` computed the composable handed to useQuery. */
function passedVariables(): Record<string, unknown> {
  const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  return (call[1] as { value: Record<string, unknown> }).value;
}

function setup(initial: ReportingPeriodType = "year") {
  queryMock.result.value = undefined;
  queryMock.error.value = null;
  const period = ref<ReportingPeriodType>(initial);
  const reporting = useSalesRepReporting({ period, organizationId: "" });
  return { period, reporting };
}

describe("useSalesRepReporting", () => {
  it("asks for every started month and its last year, and switches off the months that have not started", () => {
    setup("year");
    const variables = passedVariables();

    expect(variables.from0).toBe(new Date(2026, 0, 1).toISOString());
    expect(variables.lyFrom0).toBe(new Date(2025, 0, 1).toISOString());
    expect(variables.prevFrom).toBe(new Date(2025, 11, 1).toISOString());
    expect([variables.use9, variables.use10, variables.use11]).toEqual([true, false, false]);
    expect(Object.keys(variables).some((key) => /cancel/i.test(key))).toBe(false);
  });

  it("keeps a response with the months it was fetched for until the next one lands", async () => {
    const { period, reporting } = setup("year");
    respond({ m0: bucket(100), m9: bucket(900) });
    await nextTick();

    period.value = "6m";
    await nextTick();
    // Still the year's response, read against the year's months: January stays January.
    expect(reporting.reportPeriod.value).toBe("year");
    expect(reporting.months.value).toHaveLength(12);
    expect(reporting.months.value[0]).toMatchObject({ month: 0, revenue: 100 });

    respond({ m0: bucket(500), m5: bucket(1000) });
    await nextTick();
    expect(reporting.reportPeriod.value).toBe("6m");
    expect(reporting.months.value).toHaveLength(6);
    expect(reporting.months.value[0]).toMatchObject({ month: 4, revenue: 500 });
    expect(reporting.months.value.at(-1)).toMatchObject({ month: 9, state: "current", revenue: 1000 });
  });

  it("compares the first month with the month before the period", async () => {
    const { reporting } = setup("q1");
    respond({ prev: bucket(1000, 4), m0: bucket(1200, 4) });
    await nextTick();

    expect(reporting.months.value[0].revenueMonthChange).toBe(20);
  });

  it("takes the currency from the response", async () => {
    const { reporting } = setup();
    expect(reporting.currencyCode.value).toBe("USD");

    respond({});
    await nextTick();
    expect(reporting.currencyCode.value).toBe("EUR");
  });

  it("reports a failed read", () => {
    const { reporting } = setup();
    queryMock.error.value = new Error("boom");

    expect(reporting.error.value?.message).toBe("boom");
  });
});
