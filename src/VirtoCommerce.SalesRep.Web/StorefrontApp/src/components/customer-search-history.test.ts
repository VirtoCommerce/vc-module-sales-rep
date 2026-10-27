import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { toValue } from "vue";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerInsightsPanel from "./customer-insights-panel.vue";
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

// The panel's states, skeletons and caveat are the shell's: customer-insights-panel.test.ts.
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

const panel = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findComponent(CustomerInsightsPanel);

beforeEach(() => {
  state.items.value = [];
  state.unavailable.value = false;
  state.loading.value = false;
  state.error.value = null;
  periodFilter.initial = undefined;
});

describe("CustomerSearchHistory", () => {
  it("gives the panel its own wording, icon and state", () => {
    state.items.value = [{ term: "coffee", count: 3 }];
    state.error.value = new Error("boom");

    expect(panel(createWrapper()).props()).toMatchObject({
      icon: "search",
      failedText: "sales_rep.customer_insights.search_history.load_failed",
      emptyText: "sales_rep.customer_insights.search_history.empty_this_year",
      hasRows: true,
      failed: true,
      unavailable: false,
      loading: false,
    });
  });

  it("reads this year, which the panel names", () => {
    createWrapper();

    expect(periodFilter.initial).toBe("year");
  });

  // Recent is the baseline here as it is for product views: the newest searches are what the customer
  // is asking about now, and "top" is the deliberate second look.
  it("asks for the newest searches by default, and the ranked ones from Top", async () => {
    const wrapper = createWrapper();

    expect(toValue(searchOptions.last?.sort)).toBe("date");

    await panel(wrapper).vm.$emit("update:sort", "count");

    expect(toValue(searchOptions.last?.sort)).toBe("count");
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
});
