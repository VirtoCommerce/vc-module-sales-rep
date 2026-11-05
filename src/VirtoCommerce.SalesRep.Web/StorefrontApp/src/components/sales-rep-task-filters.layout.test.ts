import { mount } from "@vue/test-utils";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { emptyTaskFilters } from "../tasks";
import { cascadedValue, compiledStyleRules, toPx } from "../test-utils/compiled-style";
import SalesRepTaskFilters from "./sales-rep-task-filters.vue";
import type { CompiledRuleType } from "../test-utils/compiled-style";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));

const SelectStub = { name: "VcSelect", template: '<div class="select" />' };

// The width the Tasks page leaves the filter row, measured on the stand at each viewport.
const ROWS = [
  { viewport: 640, row: 577 },
  { viewport: 678, row: 615 },
  { viewport: 768, row: 485 },
  { viewport: 898, row: 615 },
];

// The row's flex layout from its rendered classes and compiled CSS; a select's floor is its width when it cannot shrink.
function rowLayout(rules: CompiledRuleType[], viewport: number) {
  const wrapper = mount(SalesRepTaskFilters, {
    props: { modelValue: emptyTaskFilters(), statuses: [], customers: [] },
    global: { stubs: { VcSelect: SelectStub } },
  });
  const rowClasses = wrapper.classes();
  const selects = wrapper.findAll(".select").map((select) => select.classes());

  const value = (classes: string[], property: string) => cascadedValue(rules, classes, property, viewport);

  return {
    direction: value(rowClasses, "flex-direction") ?? "row",
    wrap: value(rowClasses, "flex-wrap") ?? "nowrap",
    gap: toPx(value(rowClasses, "gap")) ?? 0,
    floors: selects.map((classes) =>
      value(classes, "flex-shrink") === "0"
        ? (toPx(value(classes, "width")) ?? 0)
        : (toPx(value(classes, "min-width")) ?? 0),
    ),
  };
}

describe("SalesRepTaskFilters layout", () => {
  let rules: CompiledRuleType[];

  beforeAll(async () => {
    rules = await compiledStyleRules("components/sales-rep-task-filters.vue");
  }, 30_000);

  // A select's real floor is its content, which this model cannot see, so shrinking alone does not prove a fit.
  it.each(ROWS)("wraps the row at $viewport px", ({ viewport }) => {
    expect(rowLayout(rules, viewport).wrap).toBe("wrap");
  });

  it.each(ROWS)("keeps the three filters inside a $row px row at $viewport px", ({ viewport, row }) => {
    const layout = rowLayout(rules, viewport);

    expect(layout.floors).toHaveLength(3);
    expect(layout.direction).toBe("row");

    // A row that wraps needs room for its widest select; one that does not needs room for all of them side by side.
    const needed =
      layout.wrap === "wrap"
        ? Math.max(...layout.floors)
        : layout.floors.reduce((sum, floor) => sum + floor, 0) + layout.gap * (layout.floors.length - 1);

    expect(needed).toBeLessThanOrEqual(row);
  });
});
