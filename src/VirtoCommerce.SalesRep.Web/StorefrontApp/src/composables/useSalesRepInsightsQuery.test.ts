import { beforeEach, describe, expect, it, vi } from "vitest";
import { SalesRepCustomerSearchTermsDocument } from "../api/graphql/types";
import { useSalesRepInsightsQuery } from "./useSalesRepInsightsQuery";
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

// What both insights ops share; each op's own rows are tested beside its wrapper.
function useInsights() {
  return useSalesRepInsightsQuery(SalesRepCustomerSearchTermsDocument, { organizationId: "org-1" }, "searchTerms");
}

beforeEach(() => {
  queryMock.result.value = undefined;
  queryMock.error.value = null;
  queryMock.loading.value = false;
});

describe("useSalesRepInsightsQuery", () => {
  // The shared Query.salesRepCustomerInsights entry let the other insights op answer for this one —
  // its isAnalyticsAvailable among them — and each write wiped this op's rows.
  it("keeps the op out of the shared cache", () => {
    useInsights();

    const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
    const options = call[2] as { fetchPolicy?: string } | undefined;
    expect(options?.fetchPolicy).toBe("no-cache");
  });

  it("flags unavailable from the backend flag, but never before a result arrives", () => {
    const { unavailable } = useInsights();

    // No result yet (loading or failed) must not read as unavailable.
    expect(unavailable.value).toBe(false);

    // One flag covers analytics absent, unconfigured, and a read that failed.
    queryMock.result.value = {
      salesRepCustomerInsights: { isAnalyticsAvailable: false, searchTerms: [] },
    };

    expect(unavailable.value).toBe(true);
  });

  // A null payload now means only that the caller may not see this customer — not that analytics is
  // unavailable. Reading it as unavailable would put the wrong message on a rep with no organizations.
  it("does not flag unavailable on a null payload", () => {
    const { unavailable } = useInsights();

    queryMock.result.value = { salesRepCustomerInsights: null } as unknown as SalesRepCustomerSearchTermsQuery;

    expect(unavailable.value).toBe(false);
  });

  it("surfaces the query error so the widget can show a failure state", () => {
    const { error } = useInsights();

    expect(error).toBe(queryMock.error);
  });
});
