import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { toReportingMonth } from "../reporting";
import ReportingChart from "./reporting-chart.vue";
import type { ReportingMonthSlotType } from "../reporting";

const PLOT_WIDTH = 900;

// jsdom has no layout: report a real width so the chart and its overlay render.
vi.mock("@vueuse/core", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@vueuse/core")>()),
  useResizeObserver: (_target: unknown, callback: (entries: Array<{ contentRect: { width: number } }>) => void) =>
    callback([{ contentRect: { width: PLOT_WIDTH } }]),
}));

vi.mock("vue-i18n", () => ({
  useI18n: () => ({ t: (key: string) => key, n: (value: number) => String(value), d: () => "Jan" }),
}));

function slot(month: number, state: ReportingMonthSlotType["state"] = "closed"): ReportingMonthSlotType {
  return { year: 2026, month, state, from: "", to: "", lyFrom: "", lyTo: "", daysPassed: 30, daysInMonth: 30 };
}

const MONTHS = [
  toReportingMonth(slot(0), { all: { amount: 1200, count: 3 }, lastYear: { amount: 1000, count: 2 } }),
  toReportingMonth(slot(1), { all: { amount: 800, count: 2 }, lastYear: { amount: 1000, count: 2 } }),
  toReportingMonth(slot(2), { all: { amount: 500, count: 1 } }),
  toReportingMonth(slot(3, "upcoming"), {}),
];

function mountChart() {
  return mount(ReportingChart, {
    props: { kind: "sales", title: "Sales", months: MONTHS, currencyCode: "USD", testId: "chart" },
    global: {
      stubs: {
        VcWidget: { template: "<div><slot name='header' /><slot /></div>" },
        VcBadge: { props: ["color"], template: "<span class='badge-stub' :data-color='color'><slot /></span>" },
        VcTooltip: { template: "<div class='tooltip-stub'><slot :trigger-props='{}' /><slot name='content' /></div>" },
        VcSwitch: true,
        VcTypography: true,
        VcLoaderOverlay: true,
        VcIcon: true,
      },
    },
  });
}

describe("ReportingChart", () => {
  it("shows the YoY change as kit badges, green up and red down, only where last year has a baseline", () => {
    const pills = mountChart().findAll(".reporting-chart__pill");

    expect(pills.map((pill) => pill.attributes("data-color"))).toEqual(["success", "danger"]);
  });

  it("puts a kit tooltip on every month with data and none on upcoming months", () => {
    expect(mountChart().findAll(".reporting-chart__hit")).toHaveLength(3);
  });
});
