import { beforeEach, describe, expect, it, vi } from "vitest";
import { computed, toValue } from "vue";
import { useSalesRepCustomerOrder } from "./useSalesRepCustomerOrder";
import type { SalesRepCustomerOrderQuery } from "../api/graphql/types";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const result = ref<SalesRepCustomerOrderQuery | undefined>(undefined);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  const onError = vi.fn();
  const useQuery = vi.fn(() => ({ result, loading, error, onError }));
  return { result, loading, error, onError, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));

// The host's order view-model runs only inside the host; what is ours is handing it the order.
const orderViewMock = vi.hoisted(() => vi.fn());

vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { storeId: "test-store", cultureName: "en-US", currencyCode: "USD" },
  useOrderView: orderViewMock,
}));

function passedVariables(): { id: string; cultureName?: string } {
  const args = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  const variables = args[1] as { value: { id: string; cultureName?: string } } | undefined;
  if (!variables) {
    throw new Error("useQuery was not called with variables");
  }
  return variables.value;
}

function orderResult(order: Record<string, unknown> | null) {
  return { salesRepCustomerOrder: order } as unknown as SalesRepCustomerOrderQuery;
}

beforeEach(() => {
  queryMock.result.value = undefined;
  queryMock.loading.value = false;
  queryMock.error.value = null;
  queryMock.useQuery.mockClear();
});

describe("useSalesRepCustomerOrder", () => {
  it("reads the order by id, localized to the active culture", () => {
    useSalesRepCustomerOrder("o-1");

    expect(passedVariables()).toEqual({ id: "o-1", cultureName: "en-US" });
  });

  it("exposes the order the endpoint returned", () => {
    const { order } = useSalesRepCustomerOrder("o-1");

    queryMock.result.value = orderResult({ id: "o-1", number: "CO260812-00002" });

    expect(order.value?.number).toBe("CO260812-00002");
  });

  it("reads a null order as not found, but only once the read has settled", () => {
    const { notFound } = useSalesRepCustomerOrder("o-1");

    queryMock.loading.value = true;
    expect(notFound.value).toBe(false);

    queryMock.loading.value = false;
    queryMock.result.value = orderResult(null);
    expect(notFound.value).toBe(true);
  });

  it("derives the item splits through the buyer-facing order page's own view-model", () => {
    const giftItems = computed(() => []);
    orderViewMock.mockReturnValueOnce({ giftItems });
    const view = useSalesRepCustomerOrder("o-1");

    queryMock.result.value = orderResult({ id: "o-1" });

    const [order] = orderViewMock.mock.lastCall as [unknown];
    expect(toValue(order)).toEqual({ id: "o-1" });
    expect(view.giftItems).toBe(giftItems);
  });
});
