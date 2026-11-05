import { mount } from "@vue/test-utils";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { emptyTaskFilters } from "../tasks";
import { cascadedValue, compiledStyleRules, toPx } from "../test-utils/compiled-style";
import SalesRepTaskFilters from "./sales-rep-task-filters.vue";
import type { CompiledRuleType } from "../test-utils/compiled-style";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));

const SelectStub = { name: "VcSelect", template: '<div class="select" />' };

// The width the Tasks page leaves the filter row, measured on the stand at each viewport; 615 and 617 straddle a fit.
const ROWS = [
  { viewport: 640, row: 577 },
  { viewport: 678, row: 615 },
  { viewport: 768, row: 485 },
  { viewport: 898, row: 615 },
  { viewport: 900, row: 617 },
  { viewport: 700, row: 637 },
  { viewport: 1280, row: 941 },
];

// The row's flex layout from its rendered classes and compiled CSS; a select's floor is its width when it cannot shrink.
// The row is the full width of the toolbar's query container.
function rowLayout(rules: CompiledRuleType[], viewport: number, row: number) {
  const wrapper = mount(SalesRepTaskFilters, {
    props: { modelValue: emptyTaskFilters(), statuses: [], customers: [] },
    global: { stubs: { VcSelect: SelectStub } },
  });
  const rowClasses = wrapper.classes();
  const selects = wrapper.findAll(".select").map((select) => select.classes());

  const value = (classes: string[], property: string, container = row) =>
    cascadedValue(rules, classes, property, viewport, container);
  const gap = (container?: number) => toPx(value(rowClasses, "gap", container)) ?? 0;

  // What the three selects take side by side, read from a container wide enough for every query to apply.
  const sideBySideWidths = selects.map((classes) => toPx(value(classes, "width", Infinity)) ?? 0);

  return {
    direction: value(rowClasses, "flex-direction") ?? "row",
    wrap: value(rowClasses, "flex-wrap") ?? "nowrap",
    gap: gap(),
    floors: selects.map((classes) =>
      value(classes, "flex-shrink") === "0"
        ? (toPx(value(classes, "width")) ?? 0)
        : (toPx(value(classes, "min-width")) ?? 0),
    ),
    sideBySideWidth:
      sideBySideWidths.reduce((sum, width) => sum + width, 0) + gap(Infinity) * (sideBySideWidths.length - 1),
  };
}

describe("SalesRepTaskFilters layout", () => {
  let rules: CompiledRuleType[];

  beforeAll(async () => {
    rules = await compiledStyleRules("components/sales-rep-task-filters.vue");
  }, 30_000);

  it.each(ROWS)("sets the selects side by side only where all three fit, in a $row px row", ({ viewport, row }) => {
    const layout = rowLayout(rules, viewport, row);

    expect(layout.direction).toBe(row >= layout.sideBySideWidth ? "row" : "column");
  });

  it.each(ROWS)("keeps the three filters inside a $row px row at $viewport px", ({ viewport, row }) => {
    const layout = rowLayout(rules, viewport, row);

    expect(layout.floors).toHaveLength(3);

    // Stacked or wrapping, each select needs room on its own; side by side without wrapping, all of them together.
    const needed =
      layout.direction !== "row" || layout.wrap === "wrap"
        ? Math.max(...layout.floors)
        : layout.floors.reduce((sum, floor) => sum + floor, 0) + layout.gap * (layout.floors.length - 1);

    expect(needed).toBeLessThanOrEqual(row);
  });
});
