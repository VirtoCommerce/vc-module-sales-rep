import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { summarizeReporting, toReportingMonth } from "../reporting";
import ReportingPanel from "./reporting-panel.vue";
import type { ReportingMonthSlotType, ReportingMonthType, ReportingPeriodType } from "../reporting";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    months: ref<ReportingMonthType[]>([]),
    loading: ref(false),
    error: ref<Error | null>(null),
    options: [] as { period: unknown; organizationId: unknown }[],
  };
});

// `t` prints its params after the key, so a spec sees which message was picked and what it was given.
vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
    n: (value: number) => String(value),
    d: () => "Oct 21",
    locale: { value: "en-US" },
  }),
}));

vi.mock("../composables/useToday", async () => {
  const { ref } = await import("vue");
  return { useToday: () => ref(new Date(2026, 9, 21).getTime()) };
});

vi.mock("../composables/useSalesRepReporting", async () => {
  const { computed, ref } = await import("vue");
  return {
    useSalesRepReporting: (options: { period: unknown; organizationId: unknown }) => {
      state.options.push(options);
      return {
        months: state.months,
        summary: computed(() => summarizeReporting(state.months.value)),
        currencyCode: ref("USD"),
        loading: state.loading,
        error: state.error,
        reportPeriod: ref("q1"),
      };
    },
  };
});

function slot(month: number, state: ReportingMonthSlotType["state"]): ReportingMonthSlotType {
  return { year: 2026, month, state, from: "", to: "", lyFrom: "", lyTo: "", daysPassed: 21, daysInMonth: 31 };
}

function monthsIn(...states: ReportingMonthSlotType["state"][]): ReportingMonthType[] {
  return states.map((monthState, i) => toReportingMonth(slot(i, monthState), { totals: { amount: 100, count: 1 } }));
}

type PanelPropsType = { organizationId?: string; period?: ReportingPeriodType; compact?: boolean; withTable?: boolean };

function mountPanel(props: PanelPropsType = {}, slots = {}) {
  return mount(ReportingPanel, {
    props: { organizationId: "", period: "q1", ...props },
    slots,
    global: {
      stubs: {
        ReportingChart: { props: ["loading", "kind"], template: "<div class='chart-stub' :data-loading='loading' />" },
        ReportingKpiTile: true,
        ReportingTable: { template: "<div class='table-stub' />" },
        VcTabSwitch: {
          props: ["value"],
          emits: ["change"],
          template: "<button class='period-tab' @click=\"$emit('change', value)\">{{ value }}</button>",
        },
        VcIcon: true,
        VcLoaderOverlay: true,
        VcEmptyView: { props: ["text"], template: "<div class='empty-stub'>{{ text }}</div>" },
      },
    },
  });
}

beforeEach(() => {
  state.months.value = monthsIn("closed", "closed", "current");
  state.loading.value = false;
  state.error.value = null;
  state.options = [];
});

describe("ReportingPanel", () => {
  describe("the footnote", () => {
    it("names today when the period runs into the current month", () => {
      expect(mountPanel().find(".reporting-panel__footnote").text()).toMatch(/^sales_rep\.reporting\.footnote \{/);
    });

    it("leaves today out when every month of the period has closed", () => {
      state.months.value = monthsIn("closed", "closed", "closed");

      expect(mountPanel().find(".reporting-panel__footnote").text()).toMatch(/^sales_rep\.reporting\.footnote_closed/);
    });

    it("says the period has not started when no month has", () => {
      state.months.value = monthsIn("upcoming", "upcoming", "upcoming");

      expect(mountPanel().find(".reporting-panel__footnote").text()).toBe("sales_rep.reporting.footnote_upcoming");
    });
  });

  it("shows both charts loading while a new period loads", () => {
    state.loading.value = true;

    expect(
      mountPanel()
        .findAll(".chart-stub")
        .map((chart) => chart.attributes("data-loading")),
    ).toEqual(["true", "true"]);
  });

  it("replaces the report with an error once a failed request settles", () => {
    state.error.value = new Error("boom");
    const wrapper = mountPanel();

    expect(wrapper.find(".empty-stub").text()).toBe("sales_rep.reporting.load_failed");
    expect(wrapper.find(".chart-stub").exists()).toBe(false);
  });

  it("reports on the customer it is given", async () => {
    const wrapper = mountPanel({ organizationId: "org-1" });
    const organizationId = state.options[0].organizationId as () => string;

    expect(organizationId()).toBe("org-1");

    await wrapper.setProps({ organizationId: "" });

    expect(organizationId()).toBe("");
  });

  it("hands a picked period back to its owner", async () => {
    const wrapper = mountPanel();

    await wrapper.findAll(".period-tab")[0].trigger("click");

    expect(wrapper.emitted("update:period")).toEqual([["6m"]]);
  });

  it("renders the owner's controls beside the periods", () => {
    const wrapper = mountPanel({}, { actions: "<select class='owner-control' />" });

    expect(wrapper.find(".reporting-panel__actions .owner-control").exists()).toBe(true);
  });

  it("leaves no empty actions box without them", () => {
    expect(mountPanel().find(".reporting-panel__actions").exists()).toBe(false);
  });

  it("offers the table by default", () => {
    expect(mountPanel().find(".table-stub").exists()).toBe(true);
  });

  it("leaves the table out on request", () => {
    expect(mountPanel({ withTable: false }).find(".table-stub").exists()).toBe(false);
  });

  it("marks itself compact for a narrow column", () => {
    expect(mountPanel({ compact: true }).classes()).toContain("reporting-panel--compact");
    expect(mountPanel().classes()).not.toContain("reporting-panel--compact");
  });
});
