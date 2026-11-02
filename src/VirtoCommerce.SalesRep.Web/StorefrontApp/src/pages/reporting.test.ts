import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { summarizeReporting, toReportingMonth } from "../reporting";
import Reporting from "./reporting.vue";
import type { ReportingMonthSlotType, ReportingMonthType } from "../reporting";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    months: ref<ReportingMonthType[]>([]),
    loading: ref(false),
    customersLoading: ref(false),
    customersFailed: ref(false),
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

vi.mock("../composables/useSalesRepCustomerOptions", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepCustomerOptions: () => ({
      options: ref([]),
      totalCount: ref(250),
      loading: state.customersLoading,
      failed: state.customersFailed,
      findOption: () => undefined,
    }),
  };
});

vi.mock("../composables/useReportingQueryParams", async () => {
  const { ref } = await import("vue");
  return { useReportingQueryParams: () => ({ period: ref("q1"), customerId: ref("") }) };
});

vi.mock("../composables/useSalesRepReporting", async () => {
  const { computed, ref } = await import("vue");
  return {
    useSalesRepReporting: () => ({
      months: state.months,
      summary: computed(() => summarizeReporting(state.months.value)),
      currencyCode: ref("USD"),
      loading: state.loading,
      error: ref(null),
      reportPeriod: ref("q1"),
    }),
  };
});

function slot(month: number, state: ReportingMonthSlotType["state"]): ReportingMonthSlotType {
  return { year: 2026, month, state, from: "", to: "", lyFrom: "", lyTo: "", daysPassed: 21, daysInMonth: 31 };
}

function monthsIn(...states: ReportingMonthSlotType["state"][]): ReportingMonthType[] {
  return states.map((monthState, i) => toReportingMonth(slot(i, monthState), { totals: { amount: 100, count: 1 } }));
}

function mountPage() {
  return mount(Reporting, {
    global: {
      stubs: {
        VcWidget: { template: "<div><slot name='header' /><slot /></div>" },
        VcSelect: {
          props: ["items"],
          template: "<ul><li v-for='item in items' class='customer-option'>{{ item.organizationName }}</li></ul>",
        },
        ReportingChart: { props: ["loading", "kind"], template: "<div class='chart-stub' :data-loading='loading' />" },
        ReportingKpiTile: true,
        ReportingTable: true,
        VcTabSwitch: true,
        VcTypography: true,
        VcLink: true,
        VcIcon: true,
        VcLoaderOverlay: true,
        VcEmptyView: true,
      },
    },
  });
}

beforeEach(() => {
  state.months.value = monthsIn("closed", "closed", "current");
  state.loading.value = false;
  state.customersLoading.value = false;
  state.customersFailed.value = false;
});

describe("Reporting page", () => {
  describe("the footnote", () => {
    it("names today when the period runs into the current month", () => {
      expect(mountPage().find(".sales-rep-reporting__footnote").text()).toMatch(/^sales_rep\.reporting\.footnote \{/);
    });

    it("leaves today out when every month of the period has closed", () => {
      state.months.value = monthsIn("closed", "closed", "closed");

      expect(mountPage().find(".sales-rep-reporting__footnote").text()).toMatch(
        /^sales_rep\.reporting\.footnote_closed/,
      );
    });

    it("says the period has not started when no month has", () => {
      state.months.value = monthsIn("upcoming", "upcoming", "upcoming");

      expect(mountPage().find(".sales-rep-reporting__footnote").text()).toBe("sales_rep.reporting.footnote_upcoming");
    });
  });

  describe("the All my customers option", () => {
    it("counts every served customer once the list has loaded", () => {
      expect(mountPage().find(".customer-option").text()).toBe('sales_rep.reporting.all_customers {"count":"250"}');
    });

    it.each([
      ["loading", state.customersLoading],
      ["failed", state.customersFailed],
    ])("carries no count while the list is %s", (_name, flag) => {
      flag.value = true;

      expect(mountPage().find(".customer-option").text()).toBe("sales_rep.reporting.all_customers_plain");
    });
  });

  it("shows both charts loading while a new period loads", () => {
    state.loading.value = true;

    expect(
      mountPage()
        .findAll(".chart-stub")
        .map((chart) => chart.attributes("data-loading")),
    ).toEqual(["true", "true"]);
  });
});
