import { beforeEach, describe, expect, it, vi } from "vitest";
import { startOfLocalDayIso } from "../tasks";
import { useSalesRepOverdueTaskCount } from "./useSalesRepTaskCounts";

type CountsResultType = Record<string, { totalCount?: number } | undefined>;

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref: hoistedRef } = await import("vue");
  const result = hoistedRef<CountsResultType | undefined>(undefined);
  const useQuery = vi.fn(() => ({
    result,
    loading: hoistedRef(false),
    error: hoistedRef(null),
    onError: vi.fn(),
    refetch: vi.fn(),
  }));
  return { result, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));

beforeEach(() => {
  queryMock.result.value = undefined;
  queryMock.useQuery.mockClear();
});

describe("useSalesRepOverdueTaskCount", () => {
  it("reads the overdue total", () => {
    queryMock.result.value = { overdue: { totalCount: 4 } };

    const { overdueCount } = useSalesRepOverdueTaskCount();

    expect(overdueCount.value).toBe(4);
  });

  it("reads zero until the query answers", () => {
    const { overdueCount } = useSalesRepOverdueTaskCount();

    expect(overdueCount.value).toBe(0);
  });

  // Overdue ends where the rep's own day starts, the boundary the list's status pills are drawn on.
  it("splits overdue from upcoming at the start of the rep's day", () => {
    useSalesRepOverdueTaskCount();

    const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
    const variables = (call[1] as { value: { today: string } }).value;

    expect(variables.today).toBe(startOfLocalDayIso());
  });
});
