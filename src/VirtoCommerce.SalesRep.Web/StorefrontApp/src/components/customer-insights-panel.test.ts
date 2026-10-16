import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerInsightsPanel from "./customer-insights-panel.vue";

const BASE_PROPS = {
  sort: "date",
  loading: false,
  failed: false,
  unavailable: false,
  hasRows: false,
  icon: "search",
  failedText: "load failed",
  emptyText: "nothing this year",
};

const createWrapper = createWrapperFactory(mount, CustomerInsightsPanel, {
  slots: { default: '<li data-test-id="row">gloves</li>' },
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcEmptyView: true,
      SalesRepRuleChips: {
        name: "RuleChipsStub",
        props: ["modelValue", "rules", "allLabel"],
        emits: ["update:modelValue"],
        template: "<div />",
      },
    },
  },
});

// The factory types its overrides as the whole props object; a test names only the props it varies.
function mountPanel(props: Partial<typeof BASE_PROPS> & { tallRows?: boolean } = {}) {
  return createWrapper({ props: { ...BASE_PROPS, ...props } });
}

const emptyViews = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll("vc-empty-view-stub");
const rows = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll('[data-test-id="row"]');
const skeletons = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="customer-insights-panel-skeleton"]');

describe("CustomerInsightsPanel states", () => {
  it("shows the no-data view, not an error, when nothing was tracked", () => {
    const views = emptyViews(mountPanel());

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBeUndefined();
    expect(views[0].attributes("text")).toBe("nothing this year");
    expect(views[0].attributes("icon")).toBe("search");
  });

  it("names the window beside the rows", () => {
    const wrapper = mountPanel({ hasRows: true });

    expect(wrapper.get('[data-test-id="customer-insights-panel-period-label"]').text()).toBe(
      "sales_rep.activity.period.year",
    );
  });

  // The backend answers false when the store has no insights provider — the widget must name that
  // state instead of pretending the customer never searched or viewed anything.
  it("names the unavailable state distinctly from the empty one", () => {
    const views = emptyViews(mountPanel({ unavailable: true }));

    expect(views).toHaveLength(1);
    expect(views[0].attributes("text")).toBe("sales_rep.customer_insights.analytics_unavailable");
    expect(views[0].attributes("icon")).toBe("search");
  });

  it("replaces the list with the failure view when the query failed but stale rows remain", () => {
    const wrapper = mountPanel({ hasRows: true, failed: true });
    const views = emptyViews(wrapper);

    expect(rows(wrapper)).toHaveLength(0);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
    expect(views[0].attributes("text")).toBe("load failed");
  });

  it("shows skeleton rows, not the failure view, while a retry is in flight", () => {
    const wrapper = mountPanel({ hasRows: true, failed: true, loading: true });

    expect(emptyViews(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper)).toHaveLength(5);
  });

  it("renders skeleton rows on first load, before any rows exist", () => {
    const wrapper = mountPanel({ loading: true });

    expect(skeletons(wrapper)).toHaveLength(5);
    expect(emptyViews(wrapper)).toHaveLength(0);
  });

  // The query keeps the previous answer until the new one lands: Recent's rows must not sit under the Top chip.
  it("replaces the shown rows with skeleton rows while the other sort loads", () => {
    const wrapper = mountPanel({ hasRows: true, loading: true });

    expect(rows(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper)).toHaveLength(5);
  });

  // Rows with a thumbnail stand taller; a short placeholder under them would make the list jump.
  it("makes the skeleton rows as tall as the rows they stand in for", () => {
    const short = skeletons(mountPanel({ loading: true }));
    const tall = skeletons(mountPanel({ loading: true, tallRows: true }));

    expect(short[0].classes()).not.toContain("customer-insights-panel__skeleton--tall");
    expect(tall[0].classes()).toContain("customer-insights-panel__skeleton--tall");
  });

  // GA sees only tracked activity (§4.7 of the design doc); a list without the caveat reads as a record. No date
  // beside it: the newest row's date read as the data's freshness.
  it("shows the rows with the tracked-activity caveat, with no date", () => {
    const wrapper = mountPanel({ hasRows: true });
    const caveat = wrapper.find('[data-test-id="customer-insights-panel-caveat-label"]');

    expect(rows(wrapper)).toHaveLength(1);
    expect(caveat.exists()).toBe(true);
    expect(caveat.text()).toBe("sales_rep.customer_insights.tracked_caveat");
  });
});

describe("CustomerInsightsPanel sort chips", () => {
  const chips = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findComponent({ name: "RuleChipsStub" });

  // Recent is the baseline chip, so "date" shows no rule selected; Top is the one rule.
  it("shows Recent as the baseline and Top as the one rule", () => {
    const wrapper = mountPanel();

    expect(chips(wrapper).props("modelValue")).toBeUndefined();
    expect(chips(wrapper).props("allLabel")).toBe("sales_rep.customer_insights.recent");
    expect(chips(wrapper).props("rules")).toEqual([{ name: "count", label: "sales_rep.customer_insights.top" }]);
    expect(chips(mountPanel({ sort: "count" })).props("modelValue")).toBe("count");
  });

  it("asks for the ranked list from Top, and for the newest from Recent", async () => {
    const wrapper = mountPanel({ sort: "count" });

    await chips(wrapper).vm.$emit("update:modelValue", undefined);
    await chips(wrapper).vm.$emit("update:modelValue", "count");

    expect(wrapper.emitted("update:sort")).toEqual([["date"], ["count"]]);
  });
});
