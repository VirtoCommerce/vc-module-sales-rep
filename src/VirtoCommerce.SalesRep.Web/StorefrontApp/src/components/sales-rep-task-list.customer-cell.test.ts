import { mount } from "@vue/test-utils";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { defineComponent, h, inject, provide } from "vue";
import { cascadedValue, compiledStyleRules } from "../test-utils/compiled-style";
import SalesRepTaskList from "./sales-rep-task-list.vue";
import type { CompiledRuleType } from "../test-utils/compiled-style";
import type { SalesRepTaskType } from "../types/tasks";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key, d: () => "", locale: { value: "en-US" } }) }));

// VcTable comes from the host at runtime (@vc-frontend/core ships types only), so it is stubbed: one desktop row per
// item, each column rendering its own cell template with that item.
const ROW = Symbol("row");

const RowStub = defineComponent({
  props: { item: { type: Object, required: true } },

  setup(props, { slots }) {
    provide(ROW, props.item);
    return () => h("div", slots.default?.());
  },
});

const VcTableStub = defineComponent({
  props: { items: { type: Array, default: () => [] } },

  setup(props, { slots }) {
    return () =>
      h(
        "div",
        (props.items as SalesRepTaskType[]).map((item) => h(RowStub, { item }, slots.default)),
      );
  },
});

const VcTableColumnStub = defineComponent({
  props: { id: { type: String, required: true } },

  setup(props, { slots }) {
    const item = inject(ROW);
    return () => h("div", { "data-id": props.id }, slots.default?.({ item }));
  },
});

const TASK: SalesRepTaskType = {
  id: "task-1",
  name: "Call back",
  description: "",
  type: "Finance",
  priority: "Normal",
  dueDate: "2026-10-15T00:00:00Z",
  isActive: true,
  completed: undefined,
  status: "upcoming",
  customerName: "Northwind Hospitality Group International",
};

describe("SalesRepTaskList customer cell", () => {
  let rules: CompiledRuleType[];

  beforeAll(async () => {
    rules = await compiledStyleRules("components/sales-rep-task-list.vue");
  }, 30_000);

  function customerCell() {
    const wrapper = mount(SalesRepTaskList, {
      props: { tasks: [TASK] },
      global: { stubs: { VcTable: VcTableStub, VcTableColumn: VcTableColumnStub } },
    });
    return wrapper.get('[data-id="customer"] span');
  }

  it("renders the full customer name", () => {
    expect(customerCell().text()).toBe(TASK.customerName);
  });

  it("does not clip the customer name, at any viewport", () => {
    const classes = customerCell().classes();

    for (const viewport of [1024, 1280, 1920]) {
      const value = (property: string) => cascadedValue(rules, classes, property, viewport);

      expect(value("-webkit-line-clamp")).toBeUndefined();
      expect(value("overflow")).not.toBe("hidden");
      expect(value("text-overflow")).toBeUndefined();
      expect(value("white-space")).not.toBe("nowrap");
    }
  });

  it("still lets a long word wrap inside the column", () => {
    expect(cascadedValue(rules, customerCell().classes(), "word-break", 1280)).toBe("break-word");
  });
});
