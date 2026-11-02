import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";
import { CUSTOMER_PHONES_SLOTS, useSalesRepCustomerPhones } from "./useSalesRepCustomerPhones";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref: r } = await import("vue");
  const result = r<Record<string, unknown> | undefined>(undefined);
  const loading = r(false);
  const error = r<Error | null>(null);
  const handlers = { result: [] as ((queryResult: { loading: boolean }) => void)[], error: [] as (() => void)[] };
  const onResult = vi.fn((handler: (queryResult: { loading: boolean }) => void) => handlers.result.push(handler));
  const onError = vi.fn((handler: () => void) => handlers.error.push(handler));
  const settle = () => handlers.result.forEach((handler) => handler({ loading: false }));
  const fail = () => handlers.error.forEach((handler) => handler());
  const useQuery = vi.fn(() => ({ result, loading, error, onResult, onError }));
  return { result, loading, error, handlers, settle, fail, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  Logger: { error: vi.fn() },
}));

function passedVariables(): Record<string, unknown> {
  const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  return (call[1] as { value: Record<string, unknown> }).value;
}

function passedEnabled(): boolean {
  const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  return (call[2] as { enabled: { value: boolean } }).enabled.value;
}

const details = (organizationId: string, phone?: string) => ({
  organizationId,
  organizationName: organizationId,
  phone,
});

beforeEach(() => {
  queryMock.result.value = undefined;
  queryMock.loading.value = false;
  queryMock.error.value = null;
  queryMock.useQuery.mockClear();
  queryMock.handlers.result.length = 0;
  queryMock.handlers.error.length = 0;
});

describe("useSalesRepCustomerPhones", () => {
  it("asks for every customer on the page in one request, switching the unused slots off", () => {
    useSalesRepCustomerPhones(["o1", "o2"]);
    const variables = passedVariables();

    expect(queryMock.useQuery).toHaveBeenCalledTimes(1);
    expect(variables).toMatchObject({ use0: true, id0: "o1", use1: true, id1: "o2", use2: false, id2: "" });
    expect(Object.keys(variables)).toHaveLength(CUSTOMER_PHONES_SLOTS * 2);
  });

  it("follows the page: new rows, new variables", () => {
    const ids = ref(["o1"]);
    useSalesRepCustomerPhones(() => ids.value);

    ids.value = ["o3", "o4"];

    expect(passedVariables()).toMatchObject({ id0: "o3", id1: "o4", use2: false });
  });

  it("stays idle on an empty page", () => {
    useSalesRepCustomerPhones([]);

    expect(passedEnabled()).toBe(false);
  });

  it("reports loading until an answer about this page arrives, never an empty list", () => {
    const { phonesOf } = useSalesRepCustomerPhones(["o1"]);

    // Apollo has not started yet: not loading, no result, nothing settled.
    expect(phonesOf("o1")).toEqual({ status: "loading" });
  });

  it("hands each row the numbers fetched for it", () => {
    const { phonesOf } = useSalesRepCustomerPhones(["o1", "o2"]);
    queryMock.result.value = { c0: details("o1", "+1 555 0100"), c1: details("o2") };
    queryMock.settle();

    expect(phonesOf("o1")).toEqual({
      status: "ready",
      phones: [{ name: "o1", number: "+1 555 0100", href: "tel:+15550100" }],
    });
    expect(phonesOf("o2")).toEqual({ status: "ready", phones: [] });
  });

  it("never gives a row the numbers of whoever held its slot on the previous page", () => {
    const ids = ref(["o1"]);
    const { phonesOf } = useSalesRepCustomerPhones(() => ids.value);
    queryMock.result.value = { c0: details("o1", "111") };
    queryMock.settle();

    ids.value = ["o2"];

    expect(phonesOf("o2")).toEqual({ status: "loading" });
  });

  it("reports a failed read so the row can say so", () => {
    const { phonesOf } = useSalesRepCustomerPhones(["o1"]);
    queryMock.error.value = new Error("boom");
    queryMock.fail();

    expect(phonesOf("o1")).toEqual({ status: "failed" });
  });
});
