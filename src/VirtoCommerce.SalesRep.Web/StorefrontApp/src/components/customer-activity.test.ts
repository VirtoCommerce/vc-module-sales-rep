import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { VcTabSwitch } from "@vc-frontend/core";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerActivity from "./customer-activity.vue";
import SalesRepRuleChips from "./sales-rep-rule-chips.vue";
import TrackedMetricHint from "./tracked-metric-hint.vue";
import type { SalesRepCustomerActivitySummaryType } from "../types";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    summary: ref<SalesRepCustomerActivitySummaryType | undefined>(undefined),
    loading: ref(false),
    error: ref<Error | null>(null),
  };
});

vi.mock("../composables/useSalesRepCustomerActivitySummary", () => ({
  useSalesRepCustomerActivitySummary: () => ({
    summary: state.summary,
    loading: state.loading,
    error: state.error,
  }),
}));

// The sub-view panels own their queries; here only their mount/visibility contract matters.
vi.mock("./customer-search-history.vue", () => ({
  default: { name: "CustomerSearchHistoryStub", props: ["organizationId"], template: "<div />" },
}));
vi.mock("./customer-browse-history.vue", () => ({
  default: { name: "CustomerBrowseHistoryStub", props: ["organizationId"], template: "<div />" },
}));

function summaryFixture(
  overrides: Partial<SalesRepCustomerActivitySummaryType> = {},
): SalesRepCustomerActivitySummaryType {
  return {
    createdOn: "2024-01-05T00:00:00Z",
    lastWebLogin: "2026-08-20T10:00:00Z",
    visitsCount: 12,
    lastSearchTerm: "gloves",
    // A year apart, and mid-day UTC so no timezone moves either into another year.
    lastSearchedDate: "2025-11-03T14:00:00Z",
    lastViewedProduct: { code: "SKU-1", productId: "p1", name: "Gloves" },
    lastViewedDate: "2024-10-02T14:00:00Z",
    isAnalyticsAvailable: true,
    ...overrides,
  };
}

// Slot-rendering stub: the product label lives in the link's slot, which a default stub would drop.
const VcLinkStub = { name: "VcLinkStub", props: ["to"], template: "<a><slot /></a>" };

// SalesRepRuleChips stays real — the sub-view tests below click its tabs — and so does the
// VcTabSwitch it renders them with: stubbed, the chips have no button to click.
const createWrapper = createWrapperFactory(mount, CustomerActivity, {
  props: { organizationId: "org1" },
  global: {
    renderStubDefaultSlot: false,
    components: { VcTabSwitch },
    stubs: {
      VcWidget: { template: '<div><slot name="append" /><slot name="default-container" /></div>' },
      VcButton: true,
      VcEmptyView: true,
      VcIcon: true,
      VcLink: VcLinkStub,
      // Rendered, not stubbed away: the tracked-metric hint draws its own trigger in the default slot.
      VcTooltip: { template: '<span><slot :trigger-props="{}" /></span>' },
    },
  },
});

type WrapperType = ReturnType<typeof createWrapper>;

// The chip row renders the baseline (Summary) first, then the declared rules in order.
const chips = (wrapper: WrapperType) => wrapper.findComponent(SalesRepRuleChips).findAll("button");
const rows = (wrapper: WrapperType) => wrapper.findAll('[data-test-id="customer-activity-row"]');
const summaryView = (wrapper: WrapperType) => wrapper.get('[data-test-id="customer-activity-summary-section"]');
const unavailableNote = '[data-test-id="customer-activity-unavailable-label"]';
const dateLabel = '[data-test-id="customer-activity-date-label"]';
const searchPanel = (wrapper: WrapperType) => wrapper.findComponent({ name: "CustomerSearchHistoryStub" });
const browsePanel = (wrapper: WrapperType) => wrapper.findComponent({ name: "CustomerBrowseHistoryStub" });

beforeEach(() => {
  state.summary.value = undefined;
  state.loading.value = false;
  state.error.value = null;
});

describe("CustomerActivity summary states", () => {
  it("shows the failure view when the query failed", () => {
    state.error.value = new Error("boom");

    const wrapper = createWrapper();
    const views = wrapper.findAll("vc-empty-view-stub");

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
  });

  // The GA-backed summary can run for seconds on a cold read — a blank card reads as broken.
  it("renders skeleton rows while the summary loads", () => {
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(wrapper.findAll('[data-test-id="customer-activity-skeleton"]')).toHaveLength(5);
    expect(wrapper.findAll("vc-empty-view-stub")).toHaveLength(0);
    expect(rows(wrapper)).toHaveLength(0);
  });

  it("renders every definition row when analytics is configured", () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();

    expect(rows(wrapper)).toHaveLength(5);
    expect(wrapper.find(unavailableNote).exists()).toBe(false);
    expect(wrapper.text()).toContain("SKU-1 · Gloves");
  });

  // Tracked figures appear late and load slowly, properties of the source rather than faults, so the
  // rows carrying them say so. Created on is a platform fact and carries no such mark.
  it("marks only the tracked rows", () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();
    const hints = wrapper.findAllComponents(TrackedMetricHint);

    expect(hints).toHaveLength(4);
    expect(rows(wrapper)[0].findComponent(TrackedMetricHint).exists()).toBe(false);

    // The summary is already on screen, so its hint speaks only of the delay: a load the reader has no
    // decision left to make about is noise, not a warning.
    expect(hints.map((hint) => hint.get("vc-icon-stub").attributes("label"))).toEqual(
      Array.from({ length: 4 }, () => "sales_rep.activity.tracked_hint"),
    );
  });

  // Unconfigured analytics is a distinct state, not an error and not fake zeros: the GA-sourced rows
  // give way to the note while Created on (a DB fact) keeps rendering.
  it("shows created-on plus the unavailable note when analytics is unavailable", () => {
    state.summary.value = summaryFixture({
      isAnalyticsAvailable: false,
      lastWebLogin: undefined,
      visitsCount: 0,
      lastSearchTerm: "",
      lastViewedProduct: undefined,
    });

    const wrapper = createWrapper();

    expect(rows(wrapper)).toHaveLength(1);
    expect(wrapper.find(unavailableNote).exists()).toBe(true);
    // A <dl> may hold only dt/dd groups, and the note is neither (axe definition-list).
    expect(wrapper.find(`dl ${unavailableNote}`).exists()).toBe(false);
  });

  // The quote marks differ per language, so they come with the message.
  it("quotes the last search term through the locale", () => {
    state.summary.value = summaryFixture();

    const searchRow = rows(createWrapper())[3];

    expect(searchRow.findComponent({ name: "VcLinkStub" }).text()).toBe("sales_rep.activity.quoted_term");
  });

  // By product id, never a slug: /product/{id} always resolves, whereas the tracked SEO segment alone
  // is not a valid catalog URL (VCST-5337).
  it("links the last viewed product by its id", () => {
    state.summary.value = summaryFixture();

    const productRow = rows(createWrapper())[4];

    expect(productRow.findComponent({ name: "VcLinkStub" }).props("to")).toEqual({
      name: "Product",
      params: { productId: "p1" },
    });
  });

  // An unresolvable product code still identifies the product by its code alone.
  it("renders the bare code, unlinked, when the product could not be resolved", () => {
    state.summary.value = summaryFixture({
      lastViewedProduct: { code: "GONE-1", productId: "", name: "" },
    });

    const productRow = rows(createWrapper())[4];

    expect(productRow.text()).toContain("GONE-1");
    expect(productRow.findComponent({ name: "VcLinkStub" }).exists()).toBe(false);
  });

  // The label is clamped to two lines, so the whole of it stays in the title.
  it("keeps the whole product label in the title", () => {
    state.summary.value = summaryFixture();

    const productRow = rows(createWrapper())[4];

    expect(productRow.findComponent({ name: "VcLinkStub" }).attributes("title")).toBe("SKU-1 · Gloves");
  });

  // Lifetime figures, so a "last" fact may be years old — undated, "last viewed X" beside an empty
  // this-year tab would read as a contradiction.
  it("dates the last search and the last viewed product", () => {
    state.summary.value = summaryFixture();

    const [, , , searchRow, productRow] = rows(createWrapper());

    expect(searchRow.get(dateLabel).text()).toContain("2025");
    expect(productRow.get(dateLabel).text()).toContain("2024");
  });

  it("shows no date beside a dash", () => {
    state.summary.value = summaryFixture({
      lastSearchTerm: "",
      lastSearchedDate: undefined,
      lastViewedProduct: undefined,
      lastViewedDate: undefined,
    });

    const [, , , searchRow, productRow] = rows(createWrapper());

    expect(searchRow.find(dateLabel).exists()).toBe(false);
    expect(productRow.find(dateLabel).exists()).toBe(false);
  });
});

describe("CustomerActivity sub-views", () => {
  // Opening the profile must fire only the summary query — the panels' GA-backed queries wait for
  // their chip, which means the panels must not even mount (a mounted panel fetches).
  it("defaults to Summary with neither panel mounted", () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();

    expect(summaryView(wrapper).isVisible()).toBe(true);
    expect(searchPanel(wrapper).exists()).toBe(false);
    expect(browsePanel(wrapper).exists()).toBe(false);
  });

  it("mounts the Searches panel when its chip is selected, and hides the summary", async () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();
    await chips(wrapper)[1].trigger("click");

    expect(searchPanel(wrapper).isVisible()).toBe(true);
    expect(browsePanel(wrapper).exists()).toBe(false);
    expect(summaryView(wrapper).isVisible()).toBe(false);
    expect(searchPanel(wrapper).props("organizationId")).toBe("org1");
  });

  it("mounts the Product views panel when its chip is selected", async () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();
    await chips(wrapper)[2].trigger("click");

    expect(browsePanel(wrapper).isVisible()).toBe(true);
    expect(searchPanel(wrapper).exists()).toBe(false);
    expect(browsePanel(wrapper).props("organizationId")).toBe("org1");
  });

  // Returning to a visited view must not remount its panel — a remount refires the GA-backed query.
  it("keeps a visited panel mounted, hidden, when switching back to Summary", async () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();
    await chips(wrapper)[1].trigger("click");
    await chips(wrapper)[0].trigger("click");

    expect(summaryView(wrapper).isVisible()).toBe(true);
    expect(searchPanel(wrapper).exists()).toBe(true);
    expect(searchPanel(wrapper).isVisible()).toBe(false);
  });

  // The profile keeps this instance when it moves to another customer (a history jump between two profiles).
  it("starts over on Summary, with no panel mounted, when the customer changes", async () => {
    state.summary.value = summaryFixture();

    const wrapper = createWrapper();
    await chips(wrapper)[1].trigger("click");
    await wrapper.setProps({ organizationId: "org2" });

    expect(summaryView(wrapper).isVisible()).toBe(true);
    expect(searchPanel(wrapper).exists()).toBe(false);
  });
});
