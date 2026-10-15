import { beforeEach, describe, expect, it, vi } from "vitest";
import { useSalesRepBrowseHistory } from "./useSalesRepBrowseHistory";
import type { SalesRepCustomerBrowsedProductsQuery } from "../api/graphql/types";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const result = ref<SalesRepCustomerBrowsedProductsQuery | undefined>(undefined);
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
describe("useSalesRepBrowseHistory", () => {
  it("maps rows, blanking absent display fields and the unresolved code's product id", () => {
    queryMock.result.value = {
      salesRepCustomerInsights: {
        isAnalyticsAvailable: true,
        browsedProducts: [
          { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "img", viewCount: 4 },
          // GA row the backend could not resolve: no productId.
          { sku: "CODE-2", viewCount: 1 },
        ],
      },
    } satisfies SalesRepCustomerBrowsedProductsQuery;

    const { items } = useSalesRepBrowseHistory({ organizationId: "org-1" });

    expect(items.value).toEqual([
      { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "img", viewCount: 4, lastViewedDate: undefined },
      { productId: "", name: "", sku: "CODE-2", imageUrl: "", viewCount: 1, lastViewedDate: undefined },
    ]);
  });
});
