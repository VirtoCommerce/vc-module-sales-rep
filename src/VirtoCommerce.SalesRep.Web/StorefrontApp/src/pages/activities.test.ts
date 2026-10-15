import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { nextTick, toValue } from "vue";
import { BREAKPOINTS } from "@vc-frontend/core";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import TrackedMetricHint from "../components/tracked-metric-hint.vue";
import {
  ACTIVITY_MAX_SKIP,
  ACTIVITY_PAGE_SIZE,
  CUSTOMER_PROFILE_ROUTE_NAME,
  DASHBOARD_ROUTE_NAME,
  MY_CUSTOMERS_ROUTE_NAME,
} from "../constants";
import Activities from "./activities.vue";
import type { SalesRepActivityCategoryCountType, SalesRepActivityItemType, SalesRepRuleType } from "../types";
import type { Ref } from "vue";

// The options shape the page hands the insights composables, as far as these tests assert it.
type InsightsOptionsType = {
  organizationId: () => string | undefined;
  enabled: Ref<boolean>;
};

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    items: ref<Partial<SalesRepActivityItemType>[]>([]),
    categoryCounts: ref<SalesRepActivityCategoryCountType[]>([]),
    totalCount: ref(0),
    analyticsUnavailable: ref(false),
    loading: ref(false),
    error: ref<Error | null>(null),
  };
});

// Shared state for the two ranked-insights composables (Top mode), with the options captured so the
// tests can assert what the page asks for (organizationId pass-through, enabled gating).
const insights = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    searchItems: ref<Record<string, unknown>[]>([]),
    searchUnavailable: ref(false),
    searchLoading: ref(false),
    searchError: ref<Error | null>(null),
    searchOptions: undefined as InsightsOptionsType | undefined,
    browseItems: ref<Record<string, unknown>[]>([]),
    browseUnavailable: ref(false),
    browseLoading: ref(false),
    browseError: ref<Error | null>(null),
    browseOptions: undefined as InsightsOptionsType | undefined,
  };
});

// Counting every category is the slow half of the feed, so it runs in its own request while the rows
// of the selected tab load on their own. The two are mocked apart, and the captured options let the
// tests hold the page to that split.
const counts = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    categoryCounts: ref<SalesRepActivityCategoryCountType[]>([]),
    // Category-unfiltered, so this IS the "All" figure — kept apart from the rows request's own
    // totalCount, which is category-scoped once a tab filters it.
    totalCount: ref(0),
    // The badges read this one: the two requests can disagree, and the tab row follows the counts.
    analyticsUnavailable: ref(false),
    loading: ref(false),
    // Its own, not the rows request's: a failed list must not dash the badges, nor the reverse.
    error: ref<Error | null>(null),
  };
});

type ActivityOptionsType = {
  take?: number;
  skip?: number | (() => number);
  withCategoryCounts?: boolean;
  periodFrom?: Ref<string | undefined> | (() => string | undefined);
  enabled?: () => boolean;
  keepPreviousResult?: boolean;
};

const activityCalls = vi.hoisted(() => ({ options: [] as ActivityOptionsType[] }));
// The viewport width useBreakpoints measures; jsdom has no matchMedia to ask. The mock reads the breakpoints and the
// name it is given, so the page's choice of both is what the pager tests below pin.
const viewport = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return { width: ref(1280) };
});

vi.mock("@vueuse/core", async (importOriginal) => {
  const { computed } = await import("vue");
  return {
    ...(await importOriginal<typeof import("@vueuse/core")>()),
    useBreakpoints: (breakpoints: Record<string, string>) => ({
      smaller: (name: string) => computed(() => viewport.width.value < Number.parseInt(breakpoints[name], 10)),
    }),
  };
});

// Whether the count of the rep's customers answered zero.
const customersCount = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return { hasNoCustomers: ref(false) };
});

vi.mock("../composables/useSalesRepCustomersCount", () => ({
  useSharedSalesRepCustomersCount: () => ({ hasNoCustomers: customersCount.hasNoCustomers }),
}));

// Whether the analytics module is installed and enabled; the page reads it once, when it sets up.
const analytics = vi.hoisted(() => ({ enabled: true }));
vi.mock("../composables/useSalesRepsConfig", () => ({ isSalesRepAnalyticsEnabled: () => analytics.enabled }));

vi.mock("../composables/useSalesRepActivities", () => ({
  useSalesRepActivities: (options: ActivityOptionsType = {}) => {
    activityCalls.options.push(options);

    return options.withCategoryCounts === false
      ? {
          items: state.items,
          categoryCounts: state.categoryCounts,
          totalCount: state.totalCount,
          analyticsUnavailable: state.analyticsUnavailable,
          loading: state.loading,
          error: state.error,
        }
      : {
          items: state.items,
          categoryCounts: counts.categoryCounts,
          totalCount: counts.totalCount,
          analyticsUnavailable: counts.analyticsUnavailable,
          loading: counts.loading,
          error: counts.error,
        };
  },
}));
vi.mock("../composables/useSalesRepSearchHistory", () => ({
  useSalesRepSearchHistory: (options: InsightsOptionsType) => {
    insights.searchOptions = options;
    return {
      items: insights.searchItems,
      unavailable: insights.searchUnavailable,
      loading: insights.searchLoading,
      error: insights.searchError,
    };
  },
}));
vi.mock("../composables/useSalesRepBrowseHistory", () => ({
  useSalesRepBrowseHistory: (options: InsightsOptionsType) => {
    insights.browseOptions = options;
    return {
      items: insights.browseItems,
      unavailable: insights.browseUnavailable,
      loading: insights.browseLoading,
      error: insights.browseError,
    };
  },
}));
// The heading and the breadcrumb both name the customer, so tests need to control when the name resolves.
const customerState = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    organizationName: ref<string | undefined>(undefined),
    // As apollo drives it: true from setup for a query enabled there, and only from the NEXT tick when
    // `enabled` flips on a live instance. The page's dead-end view turns on that difference.
    loading: ref(true),
    failed: ref(false),
  };
});
// `failed` and `notFound` are DERIVED here exactly as the real composable derives them - an id gates the
// read, and "not found" answers only for a read that settled on the id being asked about. Handing the page
// a free `notFound` ref would let these tests pin states the real composable cannot produce.
vi.mock("../composables/useSalesRepCustomer", async () => {
  const vue = await import("vue");
  return {
    useSalesRepCustomer: (organizationId: string | (() => string | undefined)) => {
      const enabled = vue.computed(() => Boolean(vue.toValue(organizationId)));
      const customer = vue.computed(() =>
        customerState.organizationName.value ? { organizationName: customerState.organizationName.value } : undefined,
      );
      const failed = vue.computed(() => enabled.value && customerState.failed.value);

      const answeredFor = vue.ref<string | undefined>(undefined);
      vue.watch(
        [enabled, customerState.loading, customerState.failed],
        ([isEnabled, isLoading, hasFailed], previous) => {
          const settled = hasFailed || (previous?.[1] === true && !isLoading);
          if (isEnabled && settled) {
            answeredFor.value = vue.toValue(organizationId);
          }
        },
        { immediate: true },
      );

      return {
        customer,
        loading: customerState.loading,
        failed,
        notFound: vue.computed(
          () =>
            enabled.value &&
            !customerState.loading.value &&
            !failed.value &&
            !customer.value &&
            answeredFor.value === vue.toValue(organizationId),
        ),
      };
    },
  };
});
// useBreadcrumbs is stubbed out, so capture the trail factory the page hands it and evaluate that —
// asserting the rendered stub would only prove the mock returns [].
const breadcrumbCalls = vi.hoisted(() => ({ factory: undefined as (() => unknown[]) | undefined }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useBreadcrumbs: (factory: () => unknown[]) => {
    breadcrumbCalls.factory = factory;
    return [];
  },
  usePageHead: vi.fn(),
  globals: { storeId: "test-store", cultureName: "en-US", currencyCode: "USD" },
}));

// Props-exposing stubs so the tab vocabulary and row scoping can be asserted.
const RuleChipsStub = {
  name: "RuleChipsStub",
  // Typed rather than a name list: a bare boolean attribute arrives as "" on an untyped prop. modelValue
  // is declared so props("modelValue") reads the selection; undeclared, it fell through as an attribute.
  props: { rules: Array, allLabel: String, allLast: Boolean, loading: Boolean, modelValue: String },
  // Renders the suffix slot the way the real component does — once per tab, the baseline with no
  // name — so the page's adornment rule is observable.
  template: `<div>
    <span data-name=""><slot name="suffix" :tab="{}" /></span>
    <span v-for="rule in rules" :key="rule.name" :data-name="rule.name">
      <slot name="suffix" :tab="rule" />
    </span>
  </div>`,
};
const ActivityRowStub = {
  name: "ActivityRowStub",
  props: ["item", "showOrganization", "compact"],
  template: "<div />",
};

const createWrapper = createWrapperFactory(mount, Activities, {
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcWidget: { template: '<div><slot name="default-container" /></div>' },
      VcBreadcrumbs: true,
      // Slot-rendering stub: the heading text is asserted below.
      VcTypography: { name: "VcTypographyStub", template: "<h1><slot /></h1>" },
      // Props-exposing stub: the page's own page-count arithmetic is what these tests assert.
      VcPagination: { name: "VcPaginationStub", props: ["pages", "page", "compact"], template: "<nav />" },
      // Keeps the auto-stub's element name (the assertions below read its attributes) but renders the
      // `button` slot, where the dead-end view puts its way back.
      VcEmptyView: {
        // Not "…Stub": that name would make the element below resolve back to this component.
        name: "VcEmptyView",
        props: ["text", "variant", "icon"],
        template: `<vc-empty-view-stub :text="text" :variant="variant" :icon="icon"><slot name="button" /></vc-empty-view-stub>`,
      },
      VcIcon: true,
      VcButton: { name: "VcButtonStub", props: ["to"], template: "<a><slot /></a>" },
      // Props-exposing stub so link targets can be asserted, not just their presence.
      VcLink: { name: "VcLinkStub", props: ["to"], template: "<a><slot /></a>" },
      ActivityRow: ActivityRowStub,
      SalesRepRuleChips: RuleChipsStub,
      // The tracked-metric hint draws its own trigger in the default slot, with the props the kit hands it.
      VcTooltip: { template: '<span><slot :trigger-props="{}" /></span>' },
    },
  },
});

const emptyViews = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll("vc-empty-view-stub");
const findChips = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAllComponents({ name: "RuleChipsStub" });
const findRows = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAllComponents({ name: "ActivityRowStub" });
const topRows = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="activity-top-list-row"]');
const skeletons = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="activities-skeleton"]');
const notice = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.get('[data-test-id="activities-incomplete-label"]');
const caveat = (wrapper: ReturnType<typeof createWrapper>) => wrapper.find('[data-test-id="activities-caveat-label"]');
const findPager = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findComponent({ name: "VcPaginationStub" });
const rowsSkip = () => toValue(activityCalls.options.find((options) => options.withCategoryCounts === false)?.skip);

async function goToPage(wrapper: ReturnType<typeof createWrapper>, page: number): Promise<void> {
  findPager(wrapper).vm.$emit("update:page", page);
  await nextTick();
}

// Chip order in the DOM: category tabs (0), period chips (1), then the mode toggle (2) when shown.
async function openTab(wrapper: ReturnType<typeof createWrapper>, name: string | undefined): Promise<void> {
  findChips(wrapper)[0].vm.$emit("update:modelValue", name);
  await nextTick();
  await nextTick();
}

async function switchToTop(wrapper: ReturnType<typeof createWrapper>): Promise<void> {
  findChips(wrapper)[2].vm.$emit("update:modelValue", "top");
  await nextTick();
  await nextTick();
}

beforeEach(() => {
  analytics.enabled = true;
  customersCount.hasNoCustomers.value = false;
  viewport.width.value = 1280;
  customerState.organizationName.value = undefined;
  activityCalls.options.length = 0;
  state.items.value = [];
  state.categoryCounts.value = [];
  state.totalCount.value = 0;
  state.analyticsUnavailable.value = false;
  counts.categoryCounts.value = [];
  counts.totalCount.value = 0;
  counts.analyticsUnavailable.value = false;
  counts.loading.value = false;
  counts.error.value = null;
  customerState.loading.value = true;
  customerState.failed.value = false;
  state.loading.value = false;
  state.error.value = null;
  insights.searchItems.value = [];
  insights.searchUnavailable.value = false;
  insights.searchLoading.value = false;
  insights.searchError.value = null;
  insights.searchOptions = undefined;
  insights.browseItems.value = [];
  insights.browseUnavailable.value = false;
  insights.browseLoading.value = false;
  insights.browseError.value = null;
  insights.browseOptions = undefined;
});

describe("Activities page", () => {
  // Selecting the badges is what makes the backend read every category, tracked ones included. The
  // rows request leaves them out — which is what lets a database-backed tab skip Google entirely —
  // and the badges run once, in a request that does not change as the rep switches tabs.
  it("splits the rows request from the badge-counts request", async () => {
    const wrapper = createWrapper();

    expect(activityCalls.options).toHaveLength(2);
    expect(activityCalls.options.find((options) => options.withCategoryCounts === false)?.take).toBe(
      ACTIVITY_PAGE_SIZE,
    );
    expect(activityCalls.options.find((options) => options.withCategoryCounts !== false)?.take).toBe(0);

    // A tab switch re-drives the same two reactive queries; it must not spawn more.
    await openTab(wrapper, "searches");
    expect(activityCalls.options).toHaveLength(2);
  });

  // Tracked figures appear late and load slowly, both properties of the source rather than faults, so
  // the tabs carrying them say so. Orders and Customers come from the platform's own data and do not.
  it("marks the tracked category tabs", () => {
    const marked = findChips(createWrapper())[0]
      .findAll("[data-name]")
      .filter((tab) => tab.findComponent(TrackedMetricHint).exists())
      .map((tab) => tab.attributes("data-name"));

    // "" is the All tab, which merges tracked rows in and so carries the mark too.
    expect(marked).toEqual(["", "searches", "productViews", "logins"]);
  });

  // "All time" reads the tracked categories from GA4's earliest supported date — a decade scanned to
  // render a page — so the feed opens on a bounded window and leaves the wider one a chip away.
  it("opens on a bounded period, not All time", () => {
    createWrapper();

    expect(activityCalls.options.every((options) => toValue(options.periodFrom) !== undefined)).toBe(true);
  });

  // The widgets that link here read this year, so "View all" must not open on less than they showed.
  it("opens on This year, the window of the widgets that link here", () => {
    const wrapper = createWrapper();

    expect(findChips(wrapper)[1].props("modelValue")).toBe("year");
  });

  // Zero-count categories keep their tab: a rep must see a category exists and is quiet, not wonder
  // where it went (analytics-off environments report searches/productViews/logins as 0).
  it("offers every category tab, counts included, even at zero", () => {
    counts.categoryCounts.value = [
      { category: "orders", count: 7 },
      { category: "searches", count: 0 },
    ];

    const wrapper = createWrapper();
    const categoryChips = findChips(wrapper)[0];
    const rules = categoryChips.props("rules") as SalesRepRuleType[];

    expect(rules.map((rule) => rule.name)).toEqual(["orders", "customers", "searches", "productViews", "logins"]);
    expect(rules[0].label).toContain("(7)");
    expect(rules[2].label).toContain("(0)");
    expect(rules[3].label).toBe("sales_rep.activity.tabs.product_views (0)");
  });

  // The badges count events, the counts request's totalCount counts rows — one hour bucket of 4 sign-ins is 4 on
  // the Logins tab and 1 row — so All adds up the tabs it sits beside, on every tab.
  it("adds the All badge up from the tab badges, on every tab", async () => {
    counts.categoryCounts.value = [
      { category: "orders", count: 2 },
      { category: "logins", count: 4 },
    ];
    counts.totalCount.value = 3;
    // Category-scoped once a tab filters the rows — it must never reach a badge.
    state.totalCount.value = 99;

    const wrapper = createWrapper();
    expect(findChips(wrapper)[0].props("allLabel")).toBe("sales_rep.activity.tabs.all (6)");

    await openTab(wrapper, "orders");
    expect(findChips(wrapper)[0].props("allLabel")).toBe("sales_rep.activity.tabs.all (6)");
  });

  // One source per figure: reading the selected tab's badge off the rows request instead made the
  // same tab (and All with it) change value as the rep moved between tabs.
  it("keeps a tab's badge the same whether or not it is selected", async () => {
    counts.categoryCounts.value = [{ category: "orders", count: 2 }];
    counts.totalCount.value = 2;
    state.totalCount.value = 5;

    const wrapper = createWrapper();
    const ordersBadge = () => (findChips(wrapper)[0].props("rules") as SalesRepRuleType[])[0].label;

    expect(ordersBadge()).toContain("(2)");

    await openTab(wrapper, "orders");
    expect(ordersBadge()).toContain("(2)");
  });

  it("offers the period chips beside the category tabs", () => {
    const wrapper = createWrapper();
    const chips = findChips(wrapper);

    expect(chips).toHaveLength(2);
    expect((chips[1].props("rules") as SalesRepRuleType[]).map((rule) => rule.name)).toEqual(["month", "year"]);
  });

  it("shows the tracked-activity caveat on the mixed view", () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];

    const wrapper = createWrapper();

    expect(caveat(wrapper).exists()).toBe(true);
  });

  // A failure gets its own view — it must not read as "no activity" (VCST-5586).
  it("replaces the list with the failure view when the query failed but stale rows remain", () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.error.value = new Error("boom");

    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(findRows(wrapper)).toHaveLength(0);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
  });

  // The GA-backed feed can run for seconds on a cold read — a blank widget reads as broken.
  it("shows the skeleton, not a blank widget, while the first load runs", () => {
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(skeletons(wrapper)).toHaveLength(5);
    expect(findRows(wrapper)).toHaveLength(0);
    expect(emptyViews(wrapper)).toHaveLength(0);
  });

  // keepPreviousResult keeps the outgoing tab's rows in `items` during a switch; showing them as the
  // new tab's result would mislead, so a same-height skeleton takes their place.
  it("swaps stale rows for a same-height skeleton during a tab or period refetch", () => {
    state.items.value = [
      { category: "orders", type: "orderPlaced" },
      { category: "logins", type: "login" },
      { category: "searches", type: "search" },
    ];
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(skeletons(wrapper)).toHaveLength(3);
    expect(findRows(wrapper)).toHaveLength(0);
  });

  it("keeps the tabs up without figures while the counts load", () => {
    counts.loading.value = true;

    const wrapper = createWrapper();
    const rules = findChips(wrapper)[0].props("rules") as SalesRepRuleType[];

    expect(rules[0].label).toBe("sales_rep.activity.tabs.orders");
    expect(findChips(wrapper)[0].props("allLabel")).toBe("sales_rep.activity.tabs.all");
  });

  // A refetch of the same counts holds the last-known figures instead of blanking every badge; only a first load,
  // or a switch to another customer or period (below), leaves none. Tabs and pages never refetch the counts.
  it("holds the last-known counts on the tabs while a refetch runs", async () => {
    counts.categoryCounts.value = [{ category: "orders", count: 7 }];
    counts.totalCount.value = 7;

    const wrapper = createWrapper();

    counts.loading.value = true;
    await nextTick();

    const rules = findChips(wrapper)[0].props("rules") as SalesRepRuleType[];
    expect(rules[0].label).toContain("(7)");
    expect(findChips(wrapper)[0].props("allLabel")).toContain("(7)");
  });

  // A source that did not answer must not read as a genuinely quiet period — a confident "nothing happened" over
  // an absence of measurement.
  it("names the unavailable state on the feed, not a quiet period", async () => {
    const wrapper = createWrapper();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.empty_period");

    state.analyticsUnavailable.value = true;
    await nextTick();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.customer_insights.analytics_unavailable");
  });

  // Orders come from the database, so an unreadable analytics source says nothing about an empty orders
  // tab — blaming analytics there would be the same error in the other direction.
  it("leaves the database tabs' own empty wording alone when analytics is unavailable", async () => {
    state.analyticsUnavailable.value = true;

    const wrapper = createWrapper();
    await openTab(wrapper, "orders");

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.no_results");
  });

  // The badges sit in the tab row, away from the empty view, so a literal "(0)" on a tracked tab is read
  // as a measured zero by a rep looking at another tab.
  it("drops the figure from tracked badges when analytics is unavailable", () => {
    counts.categoryCounts.value = [
      { category: "orders", count: 7 },
      { category: "searches", count: 0 },
    ];
    counts.analyticsUnavailable.value = true;

    const wrapper = createWrapper();
    const rules = findChips(wrapper)[0].props("rules") as SalesRepRuleType[];

    expect(rules[0].label).toContain("(7)"); // orders — database-sourced, still a measurement
    expect(rules[2].label).toContain("(–)"); // searches
    expect(rules[3].label).toContain("(–)"); // productViews
    expect(rules[4].label).toContain("(–)"); // logins
  });

  // Failed, the counts request has a figure for no tab — "(0)" on a first load, or the previous period's
  // counts held by keepPreviousResult, would both read as measurements.
  it("dashes every badge when the counts request failed", () => {
    counts.categoryCounts.value = [{ category: "orders", count: 7 }];
    counts.totalCount.value = 7;
    counts.error.value = new Error("boom");

    const wrapper = createWrapper();
    const chips = findChips(wrapper)[0];
    const rules = chips.props("rules") as SalesRepRuleType[];

    expect(rules[0].label).toContain("(–)"); // orders: database-sourced, but this request never brought it
    expect(chips.props("allLabel")).toContain("(–)");
  });

  // All adds every tab up: "All (31)" beside dashed tracked tabs reads as the whole feed.
  it("dashes the All badge along with the tracked ones when the counts lack analytics", () => {
    counts.categoryCounts.value = [
      { category: "orders", count: 24 },
      { category: "customers", count: 5 },
    ];
    counts.totalCount.value = 31;
    counts.analyticsUnavailable.value = true;

    const wrapper = createWrapper();

    expect(findChips(wrapper)[0].props("allLabel")).toContain("(–)");
  });

  // The counts read can lose analytics while the rows read keeps it: tracked rows beside dashed badges, with
  // nothing saying why.
  it("says the figures are incomplete beside rows when either read lacks analytics", () => {
    state.items.value = [{ category: "searches", type: "search" }];
    counts.analyticsUnavailable.value = true;

    const wrapper = createWrapper();

    expect(notice(wrapper).text()).toBe("sales_rep.activity.incomplete");
  });

  it("says nothing extra when both reads have analytics", () => {
    state.items.value = [{ category: "searches", type: "search" }];

    const wrapper = createWrapper();

    expect(notice(wrapper).text()).toBe("");
  });

  // A live region that appears together with its text is not reliably announced, so it is there from the start.
  it("keeps the notice's live region mounted while it has nothing to say", async () => {
    state.items.value = [{ category: "searches", type: "search" }];

    const wrapper = createWrapper();
    const region = notice(wrapper);
    expect(region.attributes("role")).toBe("status");

    counts.analyticsUnavailable.value = true;
    await nextTick();

    expect(notice(wrapper).element).toBe(region.element);
    expect(notice(wrapper).text()).toBe("sales_rep.activity.incomplete");
  });

  // The empty view already names the unavailable state; a second message beside it would only repeat it.
  it("leaves the notice out of an empty view", () => {
    state.analyticsUnavailable.value = true;

    const wrapper = createWrapper();

    expect(emptyViews(wrapper)).toHaveLength(1);
    expect(notice(wrapper).text()).toBe("");
  });

  // Paused in Top mode, the feed's query keeps its last answer, which says nothing about the ranked list.
  it("drops the feed's own analytics flag from the notice in Top mode", async () => {
    state.items.value = [{ category: "searches", type: "search" }];
    state.analyticsUnavailable.value = true;
    insights.searchItems.value = [{ term: "gloves", count: 5 }];

    const wrapper = createWrapper();
    await openTab(wrapper, "searches");
    expect(notice(wrapper).text()).toBe("sales_rep.activity.incomplete");

    await switchToTop(wrapper);

    expect(topRows(wrapper)).toHaveLength(1);
    expect(notice(wrapper).text()).toBe("");
  });

  // Same route, new query: the instance is reused, so the previous customer's page, tab, mode and window
  // carried over. All time carried into the rep's own feed would read every customer's history.
  it("starts over when the customer changes", async () => {
    customerState.organizationName.value = "Acme";
    state.items.value = [{ category: "searches", type: "search" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 5;
    const wrapper = createWrapper({ props: { organizationId: "org-1" } });
    const rows = activityCalls.options.find((options) => options.withCategoryCounts === false);

    await openTab(wrapper, "searches");
    findChips(wrapper)[1].vm.$emit("update:modelValue", undefined);
    await goToPage(wrapper, 4);
    await switchToTop(wrapper);
    expect(toValue(rows?.skip)).toBe(3 * ACTIVITY_PAGE_SIZE);
    expect(findChips(wrapper)[1].props("modelValue")).toBeUndefined();

    await wrapper.setProps({ organizationId: "org-2" });

    expect(findChips(wrapper)[0].props("modelValue")).toBeUndefined();
    expect(findChips(wrapper)[1].props("modelValue")).toBe("year");
    expect(toValue(rows?.skip)).toBe(0);
    expect(toValue(rows?.enabled)).toBe(true); // out of Top mode
  });

  it("returns to the first page when the tab changes", async () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 5;
    const wrapper = createWrapper();

    await goToPage(wrapper, 4);
    expect(rowsSkip()).toBe(3 * ACTIVITY_PAGE_SIZE);

    await openTab(wrapper, "orders");

    expect(rowsSkip()).toBe(0);
  });

  it("returns to the first page when the period changes", async () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 5;
    const wrapper = createWrapper();

    await goToPage(wrapper, 4);
    findChips(wrapper)[1].vm.$emit("update:modelValue", "month");
    await nextTick();

    expect(rowsSkip()).toBe(0);
  });

  // A refetch can shrink the set under the page the rep is on; the page follows it back.
  it("pulls the page back to the last one when the pages shrink below it", async () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 5;
    const wrapper = createWrapper();

    await goToPage(wrapper, 5);
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 2;
    await nextTick();

    expect(findPager(wrapper).props("page")).toBe(2);
    expect(rowsSkip()).toBe(ACTIVITY_PAGE_SIZE);
  });

  // The pager stays put while a page loads, so the button the rep just pressed keeps the focus.
  it("keeps the pager mounted while the next page loads", async () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 5;
    const wrapper = createWrapper();
    const pager = findPager(wrapper);

    state.loading.value = true;
    await goToPage(wrapper, 2);

    expect(skeletons(wrapper).length).toBeGreaterThan(0);
    expect(findPager(wrapper).exists()).toBe(true);
    expect(findPager(wrapper).vm).toBe(pager.vm);
  });

  // The backend drops analytics rows it cannot date but still counts them, so a later page can come back empty.
  it("keeps the pager when a later page comes back empty", async () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 5;
    const wrapper = createWrapper();

    await goToPage(wrapper, 4);
    state.items.value = [];
    await nextTick();

    expect(emptyViews(wrapper)).toHaveLength(1);
    expect(findPager(wrapper).exists()).toBe(true);
  });

  // A newly pressed period chip, or another customer, must not show the previous window's or customer's badges
  // until the counts for the new ones answer — seconds on a cold analytics read. The counts request holds nothing
  // across a switch, so the tabs show no figures until the new counts land.
  it("asks the counts request not to hold the previous customer's or period's figures", () => {
    createWrapper();

    expect(activityCalls.options.find((options) => options.take === 0)?.keepPreviousResult).toBe(false);
    expect(activityCalls.options.find((options) => options.take !== 0)?.keepPreviousResult).toBeUndefined();
  });

  // In Top mode the ranked list replaces the feed, so the feed's GA-backed request has nobody to answer.
  it("pauses the feed request in Top mode", async () => {
    const wrapper = createWrapper();
    const rows = activityCalls.options.find((options) => options.withCategoryCounts === false);

    await openTab(wrapper, "searches");
    expect(toValue(rows?.enabled)).toBe(true);

    await switchToTop(wrapper);
    expect(toValue(rows?.enabled)).toBe(false);
  });

  // A period-scoped feed names the tracked window; clearing the chip widens it back to lifetime,
  // where "No activity yet" is the honest wording.
  it("switches the empty-state wording with the period", async () => {
    const wrapper = createWrapper();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.empty_period");

    findChips(wrapper)[1].vm.$emit("update:modelValue", undefined);
    await nextTick();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.empty");
  });

  // A rep with no customers must not read "No tracked activity in this period", as if the customers were quiet.
  it("says the rep has no customers yet on their own feed", async () => {
    customersCount.hasNoCustomers.value = true;

    const wrapper = createWrapper();
    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.no_customers");

    await openTab(wrapper, "searches");
    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.no_customers");
  });

  // An administrator passes every customer check, so they can open a customer's feed with no customers of their own.
  it("keeps one customer's feed on its own empty wording", () => {
    customersCount.hasNoCustomers.value = true;
    customerState.organizationName.value = "Acme";

    const wrapper = createWrapper({ props: { organizationId: "org-1" } });

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.empty_period");
  });

  it("renders a row per event with the customer named on the cross-customer feed", () => {
    state.items.value = [
      { category: "orders", type: "orderPlaced" },
      { category: "logins", type: "login" },
    ];
    state.totalCount.value = 2;

    const wrapper = createWrapper();
    const rows = findRows(wrapper);

    expect(rows).toHaveLength(2);
    expect(rows[0].props("showOrganization")).toBe(true);
  });

  // The backend serves the merged feed only ACTIVITY_MAX_SKIP rows deep and returns NO rows past it,
  // while totalCount keeps counting the whole set. A pager sized from totalCount alone would offer
  // pages that come back empty and render as "no activity in this period".
  it("stops the pager at the deepest page the backend serves", () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = 10_000;

    const wrapper = createWrapper();
    const servedPages = ACTIVITY_MAX_SKIP / ACTIVITY_PAGE_SIZE + 1;

    expect(findPager(wrapper).props("pages")).toBe(servedPages);
  });

  it("sizes the pager to the rows when they fit inside that window", () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 3;

    const wrapper = createWrapper();

    expect(findPager(wrapper).props("pages")).toBe(3);
  });

  // With its labels, a two-page pager needs ~350 px, wider than a 320 px screen leaves the card.
  it("drops the pager's button labels below the sm breakpoint", () => {
    viewport.width.value = Number.parseInt(BREAKPOINTS.sm, 10) - 1;
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 2;

    const wrapper = createWrapper();

    expect(findPager(wrapper).props("compact")).toBe(true);
  });

  it("keeps the pager's button labels from the sm breakpoint up", () => {
    viewport.width.value = Number.parseInt(BREAKPOINTS.sm, 10);
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.totalCount.value = ACTIVITY_PAGE_SIZE * 2;

    const wrapper = createWrapper();

    expect(findPager(wrapper).props("compact")).toBe(false);
  });

  // The feed and the counts answer an unserved id at once, with nothing; until the customer read settles, that
  // must not show as a quiet customer with "(0)" badges.
  it("shows the loading state, not an empty zero-badge feed, until the customer resolves", () => {
    counts.categoryCounts.value = [{ category: "orders", count: 0 }];

    const wrapper = createWrapper({ props: { organizationId: "not-mine" } });

    expect(emptyViews(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper).length).toBeGreaterThan(0);
    expect(findChips(wrapper)[0].props("allLabel")).toBe("sales_rep.activity.tabs.all");
  });
});

describe("Activities page — Top|Recent mode", () => {
  it("offers the Top|Recent toggle only on the Searches and Product views tabs", async () => {
    const wrapper = createWrapper();

    expect(findChips(wrapper)).toHaveLength(2);

    await openTab(wrapper, "orders");
    expect(findChips(wrapper)).toHaveLength(2);

    await openTab(wrapper, "searches");
    const chips = findChips(wrapper);
    expect(chips).toHaveLength(3);
    expect((chips[2].props("rules") as SalesRepRuleType[]).map((rule) => rule.name)).toEqual(["top"]);
    expect(chips[2].props("allLabel")).toBe("sales_rep.customer_insights.recent");

    await openTab(wrapper, "productViews");
    expect(findChips(wrapper)).toHaveLength(3);

    await openTab(wrapper, "logins");
    expect(findChips(wrapper)).toHaveLength(2);
  });

  it("runs the ranked query only in Top mode, without organizationId on the rep-wide page", async () => {
    const wrapper = createWrapper();

    await openTab(wrapper, "searches");
    expect(toValue(insights.searchOptions!.enabled)).toBe(false);

    await switchToTop(wrapper);
    expect(toValue(insights.searchOptions!.enabled)).toBe(true);
    expect(toValue(insights.searchOptions!.organizationId)).toBeUndefined();
    expect(toValue(insights.browseOptions!.enabled)).toBe(false);
  });

  it("passes organizationId through in the per-customer page mode", async () => {
    const wrapper = createWrapper({ props: { organizationId: "org-1" } });

    await openTab(wrapper, "productViews");
    await switchToTop(wrapper);

    expect(toValue(insights.browseOptions!.enabled)).toBe(true);
    expect(toValue(insights.browseOptions!.organizationId)).toBe("org-1");
  });

  // Counts, not events: ranked rows carry no timestamps, and the capped list never pages.
  it("renders ranked search rows — rank, term link, count — with no pager", async () => {
    state.totalCount.value = 100;
    insights.searchItems.value = [
      { term: "gloves", count: 5, lastSearchedDate: "2026-08-20T00:00:00Z" },
      { term: "hats", count: 2 },
    ];

    const wrapper = createWrapper();
    await openTab(wrapper, "searches");
    await switchToTop(wrapper);

    const rows = topRows(wrapper);
    expect(rows).toHaveLength(2);
    expect(rows[0].get('[data-test-id="activity-top-list-rank-label"]').text()).toBe("1");
    expect(rows[0].findComponent({ name: "VcLinkStub" }).props("to")).toEqual({
      name: "Search",
      query: { q: "gloves" },
    });
    // The quote marks differ per language, so they come with the message.
    expect(rows[0].findComponent({ name: "VcLinkStub" }).text()).toBe("sales_rep.activity.quoted_term");
    expect(rows[0].text()).toContain("sales_rep.customer_insights.search_history.count");
    expect(rows[0].text()).not.toContain("sales_rep.customer_insights.search_history.last_searched");
    expect(findRows(wrapper)).toHaveLength(0);
    expect(findPager(wrapper).exists()).toBe(false);
    expect(caveat(wrapper).exists()).toBe(true);
  });

  // By product id, never a slug: /product/{id} always resolves. An unresolved code comes back as its
  // own productId, so it has nothing linkable and must stay plain text.
  it("deep-links a resolved product by id and degrades an unresolved code to plain text", async () => {
    insights.browseItems.value = [
      { productId: "p1", name: "Drill", sku: "D-1", isResolved: true, viewCount: 9 },
      { productId: "X-2", name: "", sku: "X-2", isResolved: false, viewCount: 3 },
    ];

    const wrapper = createWrapper();
    await openTab(wrapper, "productViews");
    await switchToTop(wrapper);

    const rows = topRows(wrapper);
    expect(rows).toHaveLength(2);
    expect(rows[0].findComponent({ name: "VcLinkStub" }).props("to")).toEqual({
      name: "Product",
      params: { productId: "p1" },
    });
    expect(rows[0].text()).toContain("Drill");
    expect(rows[1].find("a").exists()).toBe(false);
    expect(rows[1].text()).toContain("X-2");
    expect(rows[1].text()).toContain("sales_rep.customer_insights.browse_history.views");
  });

  it("shows the skeleton, not a blank widget, while the ranked query runs", async () => {
    insights.searchLoading.value = true;

    const wrapper = createWrapper();
    await openTab(wrapper, "searches");
    await switchToTop(wrapper);

    expect(skeletons(wrapper)).toHaveLength(5);
    expect(topRows(wrapper)).toHaveLength(0);
    expect(emptyViews(wrapper)).toHaveLength(0);
  });

  it("names the tracked-empty and not-configured states in Top mode", async () => {
    const wrapper = createWrapper();
    await openTab(wrapper, "searches");
    await switchToTop(wrapper);

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.customer_insights.search_history.empty");

    insights.searchUnavailable.value = true;
    await nextTick();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.customer_insights.analytics_unavailable");
  });

  // "In this period" has no period to point at under All time.
  it("names no period in Top mode under All time", async () => {
    const wrapper = createWrapper();
    findChips(wrapper)[1].vm.$emit("update:modelValue", undefined);
    await openTab(wrapper, "searches");
    await switchToTop(wrapper);

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.customer_insights.search_history.empty_all_time");

    await openTab(wrapper, "productViews");
    await switchToTop(wrapper);

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.customer_insights.browse_history.empty_all_time");
  });

  it("replaces the ranked list with the failure view when the insights query failed", async () => {
    insights.browseItems.value = [{ productId: "p1", name: "Drill", sku: "", viewCount: 1 }];
    insights.browseError.value = new Error("boom");

    const wrapper = createWrapper();
    await openTab(wrapper, "productViews");
    await switchToTop(wrapper);

    const views = emptyViews(wrapper);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
    expect(views[0].attributes("text")).toBe("sales_rep.customer_insights.browse_history.load_failed");
    expect(topRows(wrapper)).toHaveLength(0);
  });

  // Top mode swaps the list's source, not the badges' — those stay with the counts request, which the
  // mode toggle does not touch.
  it("keeps the badge figures while a Top view is shown", async () => {
    counts.categoryCounts.value = [
      { category: "searches", count: 4 },
      { category: "orders", count: 2 },
    ];
    counts.totalCount.value = 6;

    const wrapper = createWrapper();
    await openTab(wrapper, "searches");
    await switchToTop(wrapper);

    const rules = findChips(wrapper)[0].props("rules") as SalesRepRuleType[];
    expect(rules[2].label).toContain("(4)");
    expect(findChips(wrapper)[0].props("allLabel")).toContain("(6)");
  });

  it("resets to Recent when the tab switches", async () => {
    state.items.value = [{ category: "productViews", type: "productView" }];

    const wrapper = createWrapper();
    await openTab(wrapper, "searches");
    await switchToTop(wrapper);
    expect(toValue(insights.searchOptions!.enabled)).toBe(true);

    await openTab(wrapper, "productViews");

    expect(toValue(insights.searchOptions!.enabled)).toBe(false);
    expect(toValue(insights.browseOptions!.enabled)).toBe(false);
    expect(topRows(wrapper)).toHaveLength(0);
    expect(findRows(wrapper)).toHaveLength(1);
  });
});

// Not installed, or disabled from the admin UI: the page is an orders-and-customers feed and says nothing about
// tracking, rather than showing tracked activity as unavailable.
describe("Activities page without the analytics module", () => {
  beforeEach(() => {
    analytics.enabled = false;
  });

  it("offers only the platform categories, with no tracked marks", () => {
    const wrapper = createWrapper();

    expect((findChips(wrapper)[0].props("rules") as SalesRepRuleType[]).map((rule) => rule.name)).toEqual([
      "orders",
      "customers",
    ]);
    expect(wrapper.findAllComponents(TrackedMetricHint)).toHaveLength(0);
  });

  it("adds All up from the tabs it shows, and does not dash it for the missing module", () => {
    counts.categoryCounts.value = [
      { category: "orders", count: 2 },
      { category: "customers", count: 3 },
      { category: "logins", count: 4 },
    ];
    counts.analyticsUnavailable.value = true;

    const wrapper = createWrapper();

    expect(findChips(wrapper)[0].props("allLabel")).toBe("sales_rep.activity.tabs.all (5)");
  });

  it("shows no caveat and no notice about tracked activity", () => {
    state.items.value = [{ category: "orders", type: "orderPlaced" }];
    state.analyticsUnavailable.value = true;
    counts.analyticsUnavailable.value = true;

    const wrapper = createWrapper();

    expect(caveat(wrapper).exists()).toBe(false);
    expect(notice(wrapper).text()).toBe("");
  });

  it("words an empty period without tracking", () => {
    state.analyticsUnavailable.value = true;

    const wrapper = createWrapper();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.activity.empty_period_untracked");
  });
});

// i18n is mounted without messages, so t() echoes the key — asserting the key pins which string
// each mode uses, which is exactly what these three surfaces differ by.
type BreadcrumbItemType = { title: string; route?: { name: string; params?: Record<string, string> } };
const breadcrumbItems = () => (breadcrumbCalls.factory?.() ?? []) as BreadcrumbItemType[];

describe("heading and breadcrumbs", () => {
  it("names whose feed it is on the rep-wide page", () => {
    const wrapper = createWrapper();

    expect(wrapper.find("h1").text()).toBe("sales_rep.activity.page.title");
    expect(breadcrumbItems().map((x) => x.title)).toEqual([
      "common.links.account",
      "sales_rep.hub.title",
      "sales_rep.activity.breadcrumb",
    ]);
    expect(breadcrumbItems()[1].route).toEqual({ name: DASHBOARD_ROUTE_NAME });
  });

  it("names the customer in a single-line heading, with no subtitle", async () => {
    customerState.organizationName.value = "PerfOrg 000101";

    const wrapper = createWrapper({ props: { organizationId: "org-1" } });
    await nextTick();

    expect(wrapper.find("h1").text()).toBe("sales_rep.activity.page.customer_title");
  });

  it("links the customer breadcrumb to that customer's profile", async () => {
    customerState.organizationName.value = "PerfOrg 000101";

    createWrapper({ props: { organizationId: "org-1" } });
    await nextTick();

    const items = breadcrumbItems();
    expect(items.map((x) => x.title)).toEqual([
      "common.links.account",
      "sales_rep.hub.title",
      "sales_rep.my_customers.page.title",
      "PerfOrg 000101",
      "sales_rep.activity.breadcrumb",
    ]);
    expect(items[1].route).toEqual({ name: DASHBOARD_ROUTE_NAME });
    expect(items[3].route).toEqual({
      name: CUSTOMER_PROFILE_ROUTE_NAME,
      params: { organizationId: "org-1" },
    });
  });

  it("shows the bare noun and no customer crumb until the name resolves", () => {
    const wrapper = createWrapper({ props: { organizationId: "org-1" } });

    expect(wrapper.find("h1").text()).toBe("sales_rep.activity.page.title_fallback");
    expect(breadcrumbItems().map((x) => x.title)).toEqual([
      "common.links.account",
      "sales_rep.hub.title",
      "sales_rep.activity.breadcrumb",
    ]);
  });
});

// ?organizationId= is a URL a rep can arrive at with any value. Unhandled, an id they cannot see
// renders as their own feed — fallback heading, no customer crumb, every badge at 0 — while the rail
// lights nothing, and Top mode shows an empty list for what is really an access answer.
describe("a customer the rep cannot see", () => {
  it("replaces the feed with the dead-end view and a way back", async () => {
    const wrapper = createWrapper({ props: { organizationId: "not-mine" } });

    // The read settles with no customer.
    customerState.loading.value = false;
    await nextTick();

    const views = emptyViews(wrapper);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("text")).toBe("sales_rep.customer_profile.not_found");
    expect(views[0].attributes("variant")).toBe("empty");
    expect(wrapper.find("h1").exists()).toBe(false);
    expect(findChips(wrapper)).toHaveLength(0);

    expect(wrapper.findComponent({ name: "VcButtonStub" }).props("to")).toEqual({
      name: MY_CUSTOMERS_ROUTE_NAME,
    });
  });

  // A server error must not read as "this customer isn't yours".
  it("words a failed read apart from a customer that is not theirs", async () => {
    const wrapper = createWrapper({ props: { organizationId: "org-1" } });

    customerState.loading.value = false;
    customerState.failed.value = true;
    await nextTick();

    const views = emptyViews(wrapper);
    expect(views[0].attributes("text")).toBe("sales_rep.customer_profile.load_failed");
    expect(views[0].attributes("variant")).toBe("error");
  });

  // The rep-wide feed passes no id, so the customer read never runs — and a page that judged the
  // dead end from "nothing resolved" would blank the page this feature is mostly used from.
  it("leaves the rep's own feed alone", async () => {
    customerState.loading.value = false;

    const wrapper = createWrapper();
    await nextTick();

    expect(wrapper.find("h1").text()).toBe("sales_rep.activity.page.title");
    expect(findChips(wrapper).length).toBeGreaterThan(0);
  });

  // This route serves both feeds on ONE component instance, so stepping onto a customer flips the
  // customer query from disabled to enabled — and apollo defers that start by a tick. In the gap
  // nothing is loading and nothing has resolved, which is `notFound`'s own shape: judged then, the
  // dead end would flash over a customer the rep does serve.
  it("does not flash the dead end before the read for the new id starts", async () => {
    customerState.loading.value = false;

    const wrapper = createWrapper();
    await wrapper.setProps({ organizationId: "org-1" });

    expect(emptyViews(wrapper).map((view) => view.attributes("text"))).not.toContain(
      "sales_rep.customer_profile.not_found",
    );

    // The deferred read starts, then settles with no customer — now it is an answer.
    customerState.loading.value = true;
    await nextTick();
    customerState.loading.value = false;
    await nextTick();

    expect(emptyViews(wrapper)[0].attributes("text")).toBe("sales_rep.customer_profile.not_found");
  });
});
