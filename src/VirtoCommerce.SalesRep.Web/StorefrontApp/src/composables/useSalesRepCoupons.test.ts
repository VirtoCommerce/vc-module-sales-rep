import { describe, expect, it, vi } from "vitest";
import { COUPONS_DEFAULT_ROWS } from "../constants";
import { useSalesRepCoupons } from "./useSalesRepCoupons";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const result = ref<unknown>(undefined);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  const useQuery = vi.fn(() => ({ result, loading, error, onError: vi.fn() }));
  return { result, loading, error, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { storeId: "test-store", cultureName: "en-US" },
}));

type CallType = [unknown, { value: Record<string, unknown> }, Record<string, unknown>];
const lastCall = () => queryMock.useQuery.mock.calls.at(-1) as unknown as CallType;

describe("useSalesRepCoupons", () => {
  it("asks for the rep's store in the active culture, the block's cap by default", () => {
    useSalesRepCoupons();

    expect(lastCall()[1].value).toEqual({
      storeId: "test-store",
      cultureName: "en-US",
      first: COUPONS_DEFAULT_ROWS,
      after: undefined,
    });
  });

  // Paging and a new row cap change the variables; without it apollo drops the result while the next one loads,
  // the total reads 0 and the page snaps back to the first one.
  it("keeps the previous result while new variables load", () => {
    useSalesRepCoupons({ first: 10, after: "10" });

    expect(lastCall()[2]).toMatchObject({ keepPreviousResult: true });
  });

  it("names a failed read", () => {
    queryMock.error.value = new Error("boom");

    expect(useSalesRepCoupons().failed.value).toBe(true);
  });
});
