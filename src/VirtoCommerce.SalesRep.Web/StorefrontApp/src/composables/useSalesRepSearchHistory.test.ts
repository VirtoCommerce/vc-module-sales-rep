import { beforeEach, describe, expect, it, vi } from "vitest";
import { useSalesRepSearchHistory } from "./useSalesRepSearchHistory";
import type { SalesRepCustomerSearchTermsQuery } from "../api/graphql/types";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const result = ref<SalesRepCustomerSearchTermsQuery | undefined>(undefined);
  const loading = ref(false);
  const onError = vi.fn();
  const error = ref<Error | null>(null);
  const useQuery = vi.fn(() => ({ result, loading, error, onError }));
  return { result, loading, error, onError, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { storeId: "test-store", cultureName: "en-US" },
}));

beforeEach(() => {
  queryMock.result.value = undefined;
  queryMock.error.value = null;
  queryMock.loading.value = false;
});

// The flag, the cache policy and the error are the shared query's: useSalesRepInsightsQuery.test.ts.
describe("useSalesRepSearchHistory", () => {
  it("maps the payload rows and keeps counts numeric for plural selection", () => {
    queryMock.result.value = {
      salesRepCustomerInsights: {
        isAnalyticsAvailable: true,
        searchTerms: [{ term: "gloves", count: 12, lastSearchedDate: "2026-08-19T10:00:00Z" }],
      },
    } satisfies SalesRepCustomerSearchTermsQuery;

    const { items, unavailable } = useSalesRepSearchHistory({ organizationId: "org-1" });

    expect(items.value).toEqual([{ term: "gloves", count: 12, lastSearchedDate: "2026-08-19T10:00:00Z" }]);
    expect(unavailable.value).toBe(false);
  });
});
