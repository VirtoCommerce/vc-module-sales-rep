import { describe, expect, it, vi } from "vitest";
import { nextTick, ref } from "vue";
import { useReportingQueryParams } from "./useReportingQueryParams";
import type { SalesRepCustomerOptionType } from "./useSalesRepCustomerOptions";

// Each param is a plain ref seeded from `query` the way the host's getter reads the route, validator quirk included:
// an empty value skips the validator.
const query = vi.hoisted(() => ({ values: {} as Record<string, unknown> }));

vi.mock("@vc-frontend/core", async (importOriginal) => {
  const { ref } = await import("vue");
  return {
    ...(await importOriginal<Record<string, unknown>>()),
    useRouteQueryParam: (key: string, options: { defaultValue: unknown; validator?: (value: unknown) => boolean }) => {
      const raw = query.values[key] ?? null;
      let value = raw ?? options.defaultValue;
      if (raw && options.validator) {
        value = options.validator(raw) ? raw : options.defaultValue;
      }
      return ref(value);
    },
  };
});

function option(organizationId: string): SalesRepCustomerOptionType {
  return { organizationId, organizationName: organizationId, location: "", imageUrl: "" };
}

type CustomersStateType = {
  list?: SalesRepCustomerOptionType[];
  totalCount?: number;
  loading?: boolean;
  failed?: boolean;
};

function setup(values: Record<string, unknown>, state: CustomersStateType = {}) {
  const { list = [option("org-1")], totalCount = list.length, loading = false, failed = false } = state;
  query.values = values;
  const customers = { options: ref(list), totalCount: ref(totalCount), loading: ref(loading), failed: ref(failed) };
  return { customers, ...useReportingQueryParams(customers) };
}

describe("useReportingQueryParams", () => {
  it("reads a known period from the URL", () => {
    expect(setup({ period: "q2" }).period.value).toBe("q2");
  });

  it("falls back to the current year for an empty, unknown or repeated period", () => {
    expect(setup({ period: "" }).period.value).toBe("year");
    expect(setup({ period: "q9" }).period.value).toBe("year");
    expect(setup({ period: ["q1", "q2"] }).period.value).toBe("year");
  });

  it("drops a customer the rep does not serve once the full list has loaded", () => {
    expect(setup({ customer: "org-1" }).customerId.value).toBe("org-1");
    expect(setup({ customer: "org-gone" }).customerId.value).toBe("");
  });

  it("keeps an unlisted customer while the list is truncated or failed", () => {
    expect(setup({ customer: "org-200" }, { totalCount: 250 }).customerId.value).toBe("org-200");
    expect(setup({ customer: "org-2" }, { list: [], failed: true }).customerId.value).toBe("org-2");
  });

  it("waits for the list before judging the customer", async () => {
    const { customers, customerId } = setup({ customer: "org-2" }, { list: [], loading: true });
    expect(customerId.value).toBe("org-2");

    customers.options.value = [option("org-2")];
    customers.totalCount.value = 1;
    customers.loading.value = false;
    await nextTick();
    expect(customerId.value).toBe("org-2");

    customers.options.value = [option("org-3")];
    await nextTick();
    expect(customerId.value).toBe("");
  });

  it("ignores a repeated customer param", () => {
    expect(setup({ customer: ["org-1", "org-2"] }).customerId.value).toBe("");
  });
});
