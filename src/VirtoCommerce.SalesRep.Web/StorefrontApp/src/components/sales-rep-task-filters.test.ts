import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { emptyTaskFilters } from "../tasks";
import SalesRepTaskFilters from "./sales-rep-task-filters.vue";
import type { SalesRepTaskFiltersType } from "../types/tasks";

const mocks = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    options: ref<{ organizationId: string; organizationName: string; location: string; imageUrl: string }[]>([]),
    failed: ref(false),
  };
});

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("../composables/useSalesRepCustomerOptions", () => ({
  useSalesRepCustomerOptions: () => ({ options: mocks.options, failed: mocks.failed }),
}));

const SelectStub = {
  name: "VcSelect",
  props: {
    modelValue: [String, Array],
    items: Array,
    ariaLabel: String,
    placeholder: String,
    multiple: Boolean,
    clearable: Boolean,
    error: Boolean,
    message: String,
  },
  emits: ["change"],
  template: '<div class="select" :data-label="ariaLabel" />',
};

function createWrapper(modelValue: SalesRepTaskFiltersType = emptyTaskFilters()) {
  return mount(SalesRepTaskFilters, {
    props: {
      modelValue,
      statuses: [
        { name: "upcoming", label: "Upcoming" },
        { name: "overdue", label: "Overdue" },
      ],
    },
    global: { stubs: { VcSelect: SelectStub } },
  });
}

type WrapperType = ReturnType<typeof createWrapper>;

function select(wrapper: WrapperType, label: string) {
  const found = wrapper
    .findAllComponents(SelectStub)
    .find((candidate) => candidate.props("ariaLabel") === `sales_rep.tasks.filters.${label}`);
  if (!found) {
    throw new Error(`No select for ${label}`);
  }
  return found;
}

const emitted = (wrapper: WrapperType) =>
  wrapper.emitted("update:modelValue")?.at(-1)?.[0] as SalesRepTaskFiltersType | undefined;

beforeEach(() => {
  mocks.options.value = [
    { organizationId: "org-1", organizationName: "Cabin Co.", location: "", imageUrl: "" },
    { organizationId: "org-2", organizationName: "Drift Inn Resort", location: "", imageUrl: "" },
  ];
  mocks.failed.value = false;
});

describe("SalesRepTaskFilters", () => {
  it("offers the customers, the statuses and the priorities, each with its own name", () => {
    const wrapper = createWrapper();

    expect(select(wrapper, "customers_label").props("items")).toEqual(mocks.options.value);
    expect(select(wrapper, "status_label").props("items")).toEqual([
      { name: "upcoming", label: "Upcoming" },
      { name: "overdue", label: "Overdue" },
    ]);
    expect(select(wrapper, "priorities_label").props("items")).toEqual([
      { value: "Lowest", label: "sales_rep.tasks.priority.lowest" },
      { value: "Low", label: "sales_rep.tasks.priority.low" },
      { value: "Normal", label: "sales_rep.tasks.priority.normal" },
      { value: "High", label: "sales_rep.tasks.priority.high" },
      { value: "Highest", label: "sales_rep.tasks.priority.highest" },
    ]);
  });

  // Customers and priorities are multi-selects; a task has one status, and the filter takes one.
  it("takes several customers and priorities, and one status", () => {
    const wrapper = createWrapper();

    expect(select(wrapper, "customers_label").props("multiple")).toBe(true);
    expect(select(wrapper, "priorities_label").props("multiple")).toBe(true);
    expect(select(wrapper, "status_label").props("multiple")).toBeFalsy();
  });

  // The kit's multi-select holds items; the page deals in ids.
  it("shows the customers picked by id as their items", () => {
    const wrapper = createWrapper({ ...emptyTaskFilters(), customerIds: ["org-2"] });

    expect(select(wrapper, "customers_label").props("modelValue")).toEqual([mocks.options.value[1]]);
  });

  it("hands back the customers picked as ids, keeping the other filters", () => {
    const wrapper = createWrapper({ ...emptyTaskFilters(), keyword: "renewal", priorities: ["High"] });

    select(wrapper, "customers_label").vm.$emit("change", mocks.options.value);

    expect(emitted(wrapper)).toEqual({
      keyword: "renewal",
      status: undefined,
      customerIds: ["org-1", "org-2"],
      priorities: ["High"],
    });
  });

  it("hands back the priorities picked as names", () => {
    const wrapper = createWrapper();
    const items = select(wrapper, "priorities_label").props("items") as { value: string }[];

    select(wrapper, "priorities_label").vm.$emit("change", [items[3], items[4]]);

    expect(emitted(wrapper)?.priorities).toEqual(["High", "Highest"]);
  });

  it("sets the status, and clears it", () => {
    const wrapper = createWrapper();

    select(wrapper, "status_label").vm.$emit("change", "overdue");
    expect(emitted(wrapper)?.status).toBe("overdue");

    select(wrapper, "status_label").vm.$emit("change", undefined);
    expect(emitted(wrapper)?.status).toBeUndefined();
  });

  // VcSelect counts any defined value as a selection, so an unset status must not offer a Clear.
  it("offers to clear the status only when one is set", () => {
    expect(select(createWrapper(), "status_label").props("clearable")).toBe(false);
    expect(select(createWrapper({ ...emptyTaskFilters(), status: "overdue" }), "status_label").props("clearable")).toBe(
      true,
    );
  });

  // An empty list on a failed fetch reads as "this rep serves nobody"; the field says what happened instead.
  it("says so on the customer field when the customers could not be loaded", () => {
    mocks.failed.value = true;

    const wrapper = createWrapper();

    expect(select(wrapper, "customers_label").props("error")).toBe(true);
    expect(select(wrapper, "customers_label").props("message")).toBe("sales_rep.tasks.form.customer_error");
  });
});
