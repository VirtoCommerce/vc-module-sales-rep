import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";
import { emptyTaskFilters, localCalendarWindow } from "../tasks";
import { useSalesRepTaskCalendar } from "./useSalesRepTaskCalendar";
import type { SalesRepTaskFiltersType } from "../types/tasks";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref: hoistedRef } = await import("vue");
  const useQuery = vi.fn(() => ({
    result: hoistedRef(undefined),
    loading: hoistedRef(false),
    error: hoistedRef(null),
    onError: vi.fn(),
    refetch: vi.fn(),
  }));
  return { useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));

/** The reactive `variables` computed the composable handed to useQuery. */
function passedVariables(): Record<string, unknown> {
  const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  return (call[1] as { value: Record<string, unknown> }).value;
}

beforeEach(() => {
  queryMock.useQuery.mockClear();
});

describe("useSalesRepTaskCalendar", () => {
  it("asks for the month on screen", () => {
    const month = ref("2026-10-01");
    useSalesRepTaskCalendar(month);

    expect(passedVariables().period).toEqual(localCalendarWindow("2026-10-01"));

    month.value = "2026-11-01";

    expect(passedVariables().period).toEqual(localCalendarWindow("2026-11-01"));
  });

  // The dots mark where the list's tasks are, so the list's filters narrow them too; the day is what they help pick.
  it("narrows the dots by the list's filters", () => {
    const filters = ref<SalesRepTaskFiltersType>(emptyTaskFilters());
    useSalesRepTaskCalendar(ref("2026-10-01"), filters);

    filters.value = { keyword: "renew", status: "overdue", customerIds: ["org-1"], priorities: ["High", "Highest"] };

    expect(passedVariables()).toMatchObject({
      keyword: "renew",
      filter: "overdue",
      customerIds: ["org-1"],
      priorities: ["High", "Highest"],
      period: localCalendarWindow("2026-10-01"),
    });
  });

  it("sends no filter while the list has none", () => {
    useSalesRepTaskCalendar(ref("2026-10-01"), ref(emptyTaskFilters()));

    const variables = passedVariables();
    expect(variables.keyword).toBeUndefined();
    expect(variables.filter).toBeUndefined();
    expect(variables.customerIds).toBeUndefined();
    expect(variables.priorities).toBeUndefined();
  });
});
