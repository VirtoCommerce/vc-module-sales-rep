import { mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { nextTick } from "vue";
import SalesRepChart from "./sales-rep-chart.vue";
import type { ChartCategoryType, ChartSeriesType } from "./types";
import type { VueWrapper } from "@vue/test-utils";

const plotWidth = vi.hoisted(() => ({ value: 900 }));

// jsdom has no layout: report a real width so the chart and its overlay render.
vi.mock("@vueuse/core", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@vueuse/core")>()),
  useResizeObserver: (_target: unknown, callback: (entries: Array<{ contentRect: { width: number } }>) => void) =>
    callback([{ contentRect: { width: plotWidth.value } }]),
}));

const CATEGORIES: ChartCategoryType[] = [
  { id: "jan", label: "Jan", shortLabel: "J" },
  { id: "feb", label: "Feb", shortLabel: "F" },
  { id: "mar", label: "Mar", shortLabel: "M", placeholder: true },
];

const SOLID: ChartSeriesType = { id: "actual", type: "bar", name: "Actual", data: [100, 200, 300] };
const OUTLINE: ChartSeriesType = { id: "plan", type: "bar", name: "Plan", variant: "dashed", data: [150, null, null] };
const LINE: ChartSeriesType = {
  id: "trend",
  type: "line",
  name: "Trend",
  curve: "monotone",
  dots: true,
  data: [50, 80, 90],
};

let wrapper: VueWrapper | undefined;

function mountChart(props: Partial<InstanceType<typeof SalesRepChart>["$props"]> = {}, slots = {}) {
  wrapper = mount(SalesRepChart, {
    props: { categories: CATEGORIES, series: [SOLID], ariaLabel: "Sales", ...props },
    slots,
    attachTo: document.body,
    global: {
      stubs: {
        VcLoaderOverlay: true,
        VcTooltip: {
          props: ["disabled"],
          template:
            "<div class='tooltip-stub'><slot :trigger-props='{}' /><div class='tooltip-content'><slot name='content' /></div></div>",
        },
      },
    },
  });
  return wrapper;
}

afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  plotWidth.value = 900;
  vi.restoreAllMocks();
});

describe("SalesRepChart", () => {
  it("draws a bar per value and none for a placeholder category", () => {
    const bars = mountChart().findAll(".sales-rep-chart__bar");

    expect(bars).toHaveLength(2);
  });

  it("draws an overlapping outline beneath the solid bar of its column", () => {
    const chart = mountChart({ series: [SOLID, OUTLINE], barLayout: "overlap" });
    const january = chart.findAll("svg > g").at(1);
    const bars = january?.findAll(".sales-rep-chart__bar") ?? [];

    expect(bars.map((bar) => bar.classes("sales-rep-chart__bar--dashed"))).toEqual([true, false]);
    expect(bars[0]?.attributes("x")).toBe(bars[1]?.attributes("x"));
  });

  it("stands grouped bars side by side", () => {
    const chart = mountChart({ series: [SOLID, { ...OUTLINE, variant: "solid" }], barLayout: "grouped" });
    const bars = chart.findAll("svg > g").at(1)?.findAll(".sales-rep-chart__bar") ?? [];

    expect(Number(bars[1]?.attributes("x"))).toBeGreaterThan(Number(bars[0]?.attributes("x")));
  });

  it("shows a placeholder as a stub with its slot caption and no hover target", () => {
    const chart = mountChart({}, { placeholder: "Upcoming" });

    expect(chart.findAll(".sales-rep-chart__placeholder")).toHaveLength(1);
    expect(chart.find(".sales-rep-chart__placeholder-caption").text()).toBe("Upcoming");
    expect(chart.findAll(".sales-rep-chart__hit-area")).toHaveLength(2);
  });

  it("leaves a hidden series out of the plot, the axis and the legend", () => {
    const chart = mountChart({ series: [SOLID, { ...LINE, data: [5000, 5000, 5000], hidden: true }] });

    expect(chart.find(".sales-rep-chart__line").exists()).toBe(false);
    expect(chart.findAll(".sales-rep-chart__tick").at(-1)?.text()).toBe("200");
    expect(chart.findAll(".sales-rep-chart__legend-item").map((item) => item.text())).toEqual(["Actual"]);
  });

  it("lists only the series that have something drawn, then the appended items", () => {
    const empty: ChartSeriesType = { id: "none", type: "bar", name: "Nothing", data: [null, null, 400] };
    const chart = mountChart({ series: [SOLID, empty] }, { "legend-append": "<li class='extra'>Extra</li>" });

    expect(chart.findAll(".sales-rep-chart__legend-item").map((item) => item.text())).toEqual(["Actual"]);
    expect(chart.find(".sales-rep-chart__legend .extra").exists()).toBe(true);
  });

  // jsdom has no layout, so every tick label is given the same width: a short one ("$800") or a long one ("12,5 tys. USD").
  it.each([
    { labelWidth: 30, plotStart: "64" },
    { labelWidth: 71, plotStart: "79" },
  ])("starts the plot past its widest tick label ($labelWidth px)", async ({ labelWidth, plotStart }) => {
    vi.spyOn(SVGElement.prototype, "getBoundingClientRect").mockReturnValue({ width: labelWidth } as DOMRect);
    const chart = mountChart();
    await nextTick();

    expect(chart.find(".sales-rep-chart__grid line").attributes("x1")).toBe(plotStart);
  });

  it("draws a line with a smooth path and a dot per point, skipping placeholders", () => {
    const chart = mountChart({ series: [LINE] });

    expect(chart.find(".sales-rep-chart__line").attributes("d")).toContain("C");
    expect(chart.findAll(".sales-rep-chart__dot")).toHaveLength(2);
  });

  it("breaks a line at a gap instead of bridging it", () => {
    const categories = CATEGORIES.map((category) => ({ ...category, placeholder: false }));
    const chart = mountChart({ categories, series: [{ ...LINE, data: [50, null, 90] }] });

    expect(chart.find(".sales-rep-chart__line").attributes("d")?.match(/M/g)).toHaveLength(2);
    expect(chart.findAll(".sales-rep-chart__dot")).toHaveLength(2);
  });

  it("puts annotations above the columns with data, and leaves them out on narrow columns", () => {
    const wide = mountChart({}, { annotation: "<span class='note'>↑</span>" });
    expect(wide.findAll(".note")).toHaveLength(2);
    wide.unmount();

    plotWidth.value = 200;
    const narrow = mountChart({}, { annotation: "<span class='note'>↑</span>" });

    expect(narrow.findAll(".note")).toHaveLength(0);
    expect(narrow.findAll(".sales-rep-chart__label").map((label) => label.text())).toEqual(["J", "F", "M"]);
  });

  it("darkens the hovered column", async () => {
    const chart = mountChart();
    const [january] = chart.findAll(".sales-rep-chart__hit-area");

    await january.trigger("mouseenter");
    expect(chart.findAll(".sales-rep-chart__bar--active")).toHaveLength(1);

    await january.trigger("mouseleave");
    expect(chart.findAll(".sales-rep-chart__bar--active")).toHaveLength(0);
  });

  it("keeps one tab stop and moves between columns with the arrow keys", async () => {
    const chart = mountChart({ categories: [...CATEGORIES, { id: "apr", label: "Apr" }], series: [SOLID] });
    const targets = chart.findAll(".sales-rep-chart__hit-area");

    expect(targets.map((target) => target.attributes("tabindex"))).toEqual(["0", "-1", "-1"]);

    (targets[0].element as HTMLElement).focus();
    await targets[0].trigger("keydown", { key: "ArrowRight" });
    expect(document.activeElement).toBe(targets[1].element);

    // The placeholder (Mar) is skipped.
    await targets[1].trigger("keydown", { key: "ArrowRight" });
    expect(document.activeElement).toBe(targets[2].element);
    expect(targets.map((target) => target.attributes("tabindex"))).toEqual(["-1", "-1", "0"]);

    await targets[2].trigger("keydown", { key: "Home" });
    expect(document.activeElement).toBe(targets[0].element);
  });

  it("hands the tooltip the column and every visible series' value", () => {
    const chart = mountChart(
      { series: [SOLID, OUTLINE] },
      {
        tooltip:
          "<template #tooltip='{ category, values }'>{{ category.label }}: {{ values.map((v) => v.value).join('/') }}</template>",
      },
    );

    expect(chart.find(".tooltip-content").text()).toBe("Jan: 100/150");
  });
});
