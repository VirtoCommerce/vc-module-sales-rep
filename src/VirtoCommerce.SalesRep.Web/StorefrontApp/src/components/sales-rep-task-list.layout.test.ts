import { mount } from "@vue/test-utils";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { cascadedValue, compiledStyleRules, toPx } from "../test-utils/compiled-style";
import SalesRepTaskList from "./sales-rep-task-list.vue";
import type { CompiledRuleType } from "../test-utils/compiled-style";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key, d: () => "", locale: { value: "en-US" } }) }));

// The header pass alone: each column renders as one element carrying the class the page gives that column.
const VcTableStub = defineComponent({
  setup(_, { slots }) {
    return () => h("div", slots.default?.());
  },
});

const VcTableColumnStub = defineComponent({
  props: { id: { type: String, required: true } },

  setup(props) {
    return () => h("span", { class: "col", "data-id": props.id });
  },
});

// One cell alone: the Customer column renders its cell template for a task, the other columns nothing.
const CustomerCellStub = defineComponent({
  props: { id: { type: String, required: true } },

  setup(props, { slots }) {
    return () =>
      props.id === "customer" ? h("div", slots.default?.({ item: { customerName: "Northwind Hospitality" } })) : null;
  },
});

// The desktop table's width and cell padding, measured on the stand at each viewport.
const TABLES = [
  { viewport: 1280, table: 639 },
  { viewport: 1366, table: 724.9 },
  { viewport: 1440, table: 799 },
  { viewport: 1920, table: 841.9 },
];
const CELL_PADDING = 32;

// A long customer name (25 characters) in full on two lines of the cell, at the stand's text width per character.
const LONG_NAME = 25;
const PX_PER_CHARACTER = 63.7 / 9;
const READABLE_TEXT_WIDTH = Math.ceil(LONG_NAME / 2) * PX_PER_CHARACTER;

// The least text width Task and Notes may keep at 1280 px, where Customer's share comes out of theirs.
const NARROWEST_TEXT_WIDTH = 45;

// Fixed table layout: a sized column gets its width, the unsized ones split the rest equally.
function columnWidths(rules: CompiledRuleType[], viewport: number, table: number) {
  const wrapper = mount(SalesRepTaskList, {
    props: { tasks: [] },
    global: { stubs: { VcTable: VcTableStub, VcTableColumn: VcTableColumnStub } },
  });
  const columns = wrapper.findAll(".col").map((column) => ({
    id: column.attributes("data-id") as string,
    width: toPx(cascadedValue(rules, column.classes(), "width", viewport), table),
  }));
  const sized = columns.reduce((sum, column) => sum + (column.width ?? 0), 0);
  const unsized = columns.filter((column) => column.width === undefined).length;

  return new Map(columns.map((column) => [column.id, column.width ?? (table - sized) / unsized]));
}

// The classes the customer name renders with, so the checks follow the template.
function customerCellClasses(): string[] {
  const wrapper = mount(SalesRepTaskList, {
    props: { tasks: [] },
    global: { stubs: { VcTable: VcTableStub, VcTableColumn: CustomerCellStub } },
  });

  return wrapper.get("span").classes();
}

describe("SalesRepTaskList layout", () => {
  let rules: CompiledRuleType[];

  beforeAll(async () => {
    rules = await compiledStyleRules("components/sales-rep-task-list.vue");
  }, 30_000);

  it("lays the desktop table out fixed, which the column model below assumes", () => {
    const table = rules.find((rule) => rule.selector === ".sales-rep-task-list .vc-table__desktop");

    expect(table?.declarations["table-layout"]).toBe("fixed");
  });

  it.each(TABLES)("gives a long customer name room for two lines at $viewport px", ({ viewport, table }) => {
    const widths = columnWidths(rules, viewport, table);

    expect(widths.get("customer")! - CELL_PADDING).toBeGreaterThanOrEqual(READABLE_TEXT_WIDTH);
  });

  it.each(["task", "notes"])("leaves the %s column room for text at 1280 px", (id) => {
    const widths = columnWidths(rules, 1280, 639);

    expect(widths.get(id)! - CELL_PADDING).toBeGreaterThanOrEqual(NARROWEST_TEXT_WIDTH);
  });

  it("does not clip the customer name, at any viewport", () => {
    const classes = customerCellClasses();

    for (const viewport of [1024, 1280, 1920]) {
      const value = (property: string) => cascadedValue(rules, classes, property, viewport);

      expect(value("-webkit-line-clamp")).toBeUndefined();
      expect(value("overflow")).not.toBe("hidden");
      expect(value("text-overflow")).toBeUndefined();
      expect(value("white-space")).not.toBe("nowrap");
    }
  });

  it("still lets a long word wrap inside the customer column", () => {
    expect(cascadedValue(rules, customerCellClasses(), "word-break", 1280)).toBe("break-word");
  });
});
