import { describe, expect, it, vi } from "vitest";
import { ref, unref } from "vue";
import { ACTIVITY_PAGE_SIZE } from "../constants";
import { useSalesRepActivities } from "./useSalesRepActivities";
import type { SalesRepActivitiesQuery } from "../api/graphql/types";

// vi.hoisted runs before this file's imports, so it must import vue itself.
const queryMock = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const result = ref<SalesRepActivitiesQuery | undefined>(undefined);
  const loading = ref(false);
  const onError = vi.fn();
  const error = ref<Error | null>(null);
  const useQuery = vi.fn(() => ({ result, loading, error, onError }));
  return { result, loading, error, onError, useQuery };
});

vi.mock("@vue/apollo-composable", () => ({ useQuery: queryMock.useQuery }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { storeId: "test-store", cultureName: "en-US", currencyCode: "USD" },
}));

// Typed against the generated row so a fixture can't drift from the real payload shape.
type ActivityEventType = NonNullable<SalesRepActivitiesQuery["salesRepActivities"]>["items"][number];

function activityEvent(overrides: Partial<ActivityEventType> = {}): ActivityEventType {
  return {
    category: "orders",
    type: "orderPlaced",
    occurredAt: "2026-08-20T10:15:00Z",
    precision: "exact",
    count: 1,
    organizationName: "Acme",
    orderId: "ord1",
    orderNumber: "CU0001",
    orderStatus: "New",
    orderStatusDisplayValue: "New",
    orderTotal: { formattedAmount: "$100.00" },
    ...overrides,
  };
}

/** The reactive `variables` computed the composable handed to useQuery. */
function passedVariables(): Record<string, unknown> {
  const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  const variables = call[1] as { value: Record<string, unknown> } | undefined;
  if (!variables) {
    throw new Error("useQuery was not called with variables");
  }
  return variables.value;
}

/** The options the composable handed to useQuery. */
function passedOptions(): { enabled?: unknown; keepPreviousResult?: boolean } {
  const call = (queryMock.useQuery.mock.calls.at(-1) ?? []) as unknown[];
  return (call[2] ?? {}) as { enabled?: unknown; keepPreviousResult?: boolean };
}

function connection(
  items: ActivityEventType[],
  categoryCounts: { category: string; count: number }[] = [],
  isAnalyticsAvailable = true,
): SalesRepActivitiesQuery {
  return { salesRepActivities: { isAnalyticsAvailable, totalCount: items.length, categoryCounts, items } };
}

describe("useSalesRepActivities", () => {
  it("maps an order event with its formatted total and localized status", () => {
    queryMock.result.value = connection([activityEvent()]);

    const { items } = useSalesRepActivities();

    expect(items.value[0]).toMatchObject({
      category: "orders",
      type: "orderPlaced",
      precision: "exact",
      count: 1,
      orderNumber: "CU0001",
      statusDisplayValue: "New",
      orderTotal: "$100.00",
    });
  });

  // GA hour-buckets: the precision flag drives the honest "~" rendering, and a moneyless row must not
  // read as a currency zero.
  it("keeps hour precision and the bucket size, with no money on analytics rows", () => {
    queryMock.result.value = connection([
      activityEvent({
        category: "searches",
        type: "search",
        precision: "hour",
        count: 3,
        searchTerm: "gloves",
        orderId: undefined,
        orderNumber: undefined,
        orderTotal: undefined,
      }),
    ]);

    const { items } = useSalesRepActivities();

    expect(items.value[0]).toMatchObject({ precision: "hour", count: 3, searchTerm: "gloves", orderTotal: "" });
  });

  it("defaults an absent count to 1 so exact rows never render a blank occurrence figure", () => {
    queryMock.result.value = connection([activityEvent({ count: undefined })]);

    const { items } = useSalesRepActivities();

    expect(items.value[0].count).toBe(1);
  });

  it("exposes categoryCounts and totalCount for the tabs", () => {
    queryMock.result.value = {
      salesRepActivities: {
        isAnalyticsAvailable: true,
        totalCount: 7,
        categoryCounts: [
          { category: "orders", count: 7 },
          { category: "logins", count: 0 },
        ],
        items: [],
      },
    };

    const { categoryCounts, totalCount } = useSalesRepActivities({ take: 0 });

    expect(totalCount.value).toBe(7);
    expect(categoryCounts.value).toEqual([
      { category: "orders", count: 7 },
      { category: "logins", count: 0 },
    ]);
  });

  // Only an explicit false counts. A response that has not arrived, and a null field, must not report
  // "unavailable" — the surfaces render a different empty state on it, and a wrong one is the same
  // confident claim in reverse.
  it("reports analytics unavailable only on an explicit false", () => {
    queryMock.result.value = undefined;
    expect(useSalesRepActivities().analyticsUnavailable.value).toBe(false);

    queryMock.result.value = connection([], [], false);
    expect(useSalesRepActivities().analyticsUnavailable.value).toBe(true);

    queryMock.result.value = connection([activityEvent()]);
    expect(useSalesRepActivities().analyticsUnavailable.value).toBe(false);
  });

  // Paused, the query keeps its last answer, which says nothing about the view shown instead (the Activities
  // page's Top list): the incomplete-figures notice must not fire on it.
  it("reports analytics unavailable only while it reads", () => {
    queryMock.result.value = connection([], [], false);
    const reading = ref(false);

    const { analyticsUnavailable } = useSalesRepActivities({ enabled: () => reading.value });
    expect(analyticsUnavailable.value).toBe(false);

    reading.value = true;
    expect(analyticsUnavailable.value).toBe(true);
  });

  // A null field (unauthorized org / analytics off) settles to empty data, never a crash or an error state.
  it("settles to empty data when the whole field is null", () => {
    queryMock.result.value = { salesRepActivities: undefined };

    const { items, categoryCounts, totalCount } = useSalesRepActivities();

    expect(items.value).toEqual([]);
    expect(categoryCounts.value).toEqual([]);
    expect(totalCount.value).toBe(0);
  });

  it("surfaces the query error so the surfaces can show a failure state", () => {
    const { error } = useSalesRepActivities();

    expect(error).toBe(queryMock.error);
  });

  // Held by default, so a tab switch keeps the outgoing rows until the new ones land; the badges opt out.
  it("holds the previous result unless asked not to", () => {
    useSalesRepActivities();
    expect(passedOptions().keepPreviousResult).toBe(true);

    useSalesRepActivities({ keepPreviousResult: false });
    expect(passedOptions().keepPreviousResult).toBe(false);
  });
});

describe("useSalesRepActivities request", () => {
  it("asks for a first page of every category, with the badges, in the store and culture", () => {
    useSalesRepActivities();

    expect(passedVariables()).toEqual({
      organizationId: undefined,
      storeId: "test-store",
      cultureName: "en-US",
      categories: undefined,
      periodFrom: undefined,
      periodTo: undefined,
      take: ACTIVITY_PAGE_SIZE,
      skip: 0,
      withCategoryCounts: true,
    });
  });

  // take: 0 is the counts-only request behind the badges: a default for any falsy take would fetch a page of rows.
  it("keeps take: 0", () => {
    useSalesRepActivities({ take: 0 });

    expect(passedVariables().take).toBe(0);
  });

  it("leaves the badges out when asked, and passes the scope, window and page through", () => {
    useSalesRepActivities({
      organizationId: () => "org-1",
      categories: () => ["searches"],
      periodFrom: "2026-01-01T00:00:00.000Z",
      periodTo: () => "2026-12-31T23:59:59.999Z",
      take: 20,
      skip: () => 40,
      withCategoryCounts: false,
    });

    expect(passedVariables()).toMatchObject({
      organizationId: "org-1",
      categories: ["searches"],
      periodFrom: "2026-01-01T00:00:00.000Z",
      periodTo: "2026-12-31T23:59:59.999Z",
      take: 20,
      skip: 40,
      withCategoryCounts: false,
    });
  });

  // Apollo unwraps a ref inside its options but reads a getter as truthy: the feed would never pause in Top mode.
  it("hands Apollo a ref for an enabled getter", () => {
    useSalesRepActivities({ enabled: () => false });

    expect(unref(passedOptions().enabled)).toBe(false);
  });
});
