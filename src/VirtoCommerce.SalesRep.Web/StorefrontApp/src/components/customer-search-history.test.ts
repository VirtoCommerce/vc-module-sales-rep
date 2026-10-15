import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { toValue } from "vue";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerSearchHistory from "./customer-search-history.vue";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    items: ref<Record<string, unknown>[]>([]),
    unavailable: ref(false),
    loading: ref(false),
    error: ref<Error | null>(null),
  };
});

// The options are captured: the default sort and the visibility gate are part of this panel's contract.
const searchOptions = vi.hoisted(() => ({ last: undefined as Record<string, unknown> | undefined }));
vi.mock("../composables/useSalesRepSearchHistory", () => ({
  useSalesRepSearchHistory: (options: Record<string, unknown>) => {
    searchOptions.last = options;
    return { ...state };
  },
}));
// The window is captured too: the label and the empty text say "this year", so the request has to mean it.
const periodFilter = vi.hoisted(() => ({ initial: undefined as string | undefined }));
vi.mock("../composables/useSalesRepPeriodFilter", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepPeriodFilter: (initial?: string) => {
      periodFilter.initial = initial;
      return { from: ref(undefined), to: ref(undefined) };
    },
  };
});

const createWrapper = createWrapperFactory(mount, CustomerSearchHistory, {
  props: { organizationId: "org-1" },
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcEmptyView: true,
      VcIcon: true,
      VcLink: { name: "VcLinkStub", props: ["to"], template: "<a><slot /></a>" },
      SalesRepRuleChips: true,
    },
  },
});

const emptyViews = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll("vc-empty-view-stub");
const rows = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="customer-search-history-row"]');
const skeletons = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="customer-search-history-skeleton"]');

beforeEach(() => {
  state.items.value = [];
  state.unavailable.value = false;
  state.loading.value = false;
  state.error.value = null;
  periodFilter.initial = undefined;
});

describe("CustomerSearchHistory states", () => {
  it("shows the no-data view, not an error, when nothing was tracked this year", () => {
    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBeUndefined();
    expect(views[0].attributes("text")).toBe("sales_rep.customer_insights.search_history.empty_this_year");
  });

  it("reads this year and names it beside the rows", () => {
    state.items.value = [{ term: "coffee", count: 3 }];

    const wrapper = createWrapper();

    expect(periodFilter.initial).toBe("year");
    expect(wrapper.get('[data-test-id="customer-search-history-period-label"]').text()).toBe(
      "sales_rep.activity.period.year",
    );
  });

  // The backend answers null when the store has no insights provider — the widget must name that
  // state instead of pretending the customer never searched.
  it("names the unavailable state distinctly from the empty one", () => {
    state.unavailable.value = true;

    const views = emptyViews(createWrapper());

    expect(views).toHaveLength(1);
    expect(views[0].attributes("text")).toBe("sales_rep.customer_insights.analytics_unavailable");
  });

  it("replaces the list with the failure view when the query failed but stale rows remain", () => {
    state.items.value = [{ term: "gloves", count: 3 }];
    state.error.value = new Error("boom");

    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(rows(wrapper)).toHaveLength(0);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
  });

  it("shows skeleton rows, not the failure view, while a retry is in flight", () => {
    state.items.value = [{ term: "gloves", count: 3 }];
    state.error.value = new Error("boom");
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(emptyViews(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper)).toHaveLength(5);
  });

  it("renders skeleton rows on first load, before any rows exist", () => {
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(skeletons(wrapper)).toHaveLength(5);
    expect(emptyViews(wrapper)).toHaveLength(0);
  });

  // The query keeps the previous answer until the new one lands: Recent's rows must not sit under the Top chip.
  it("replaces the shown rows with skeleton rows while the other sort loads", () => {
    state.items.value = [{ term: "gloves", count: 3 }];
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(rows(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper)).toHaveLength(5);
  });

  // GA sees only tracked activity (§4.7 of the design doc); a list without the caveat reads as a record. No date
  // beside it: the newest row's date read as the data's freshness.
  it("shows the tracked-activity caveat beside the rows, with no date", () => {
    state.items.value = [{ term: "gloves", count: 3, lastSearchedDate: "2026-08-20T00:00:00Z" }];

    const caveat = createWrapper().find('[data-test-id="customer-search-history-caveat-label"]');

    expect(caveat.exists()).toBe(true);
    expect(caveat.text()).toBe("sales_rep.customer_insights.tracked_caveat");
  });

  // VCST-5731: "clicking a search term opens catalog search results for that term".
  it("links every term to the catalog search for it", () => {
    state.items.value = [{ term: "coffee", count: 3 }];

    const link = createWrapper().findComponent({ name: "VcLinkStub" });

    expect(link.exists()).toBe(true);
    expect(link.text()).toBe("coffee");
    expect(link.props("to")).toMatchObject({ query: { q: "coffee" } });
    expect(link.attributes("target")).toBe("_blank");
    expect(link.attributes("rel")).toBe("noopener noreferrer");
  });

  // Recent is the baseline here as it is for product views: the newest searches are what the customer
  // is asking about now, and "top" is the deliberate second look.
  it("asks for the newest searches by default", () => {
    createWrapper();

    expect(toValue(searchOptions.last?.sort)).toBe("date");
  });
});
