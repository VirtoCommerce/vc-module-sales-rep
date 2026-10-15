import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SalesRepTaskCustomerSelect from "./sales-rep-task-customer-select.vue";

const mocks = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    options: ref<{ organizationId: string; organizationName: string; location: string; imageUrl: string }[]>([]),
    failed: ref(false),
  };
});

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("../composables/useSalesRepCustomerOptions", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepCustomerOptions: () => ({
      options: mocks.options,
      failed: mocks.failed,
      loading: ref(false),
      findOption: vi.fn(),
    }),
  };
});

const SelectStub = {
  props: ["modelValue", "items", "label", "clearable", "error", "message"],
  emits: ["update:modelValue"],
  template: '<div class="select" />',
};

function createWrapper(props: { modelValue: string; name?: string }) {
  return mount(SalesRepTaskCustomerSelect, { props, global: { stubs: { VcSelect: SelectStub } } });
}

const select = (wrapper: ReturnType<typeof createWrapper>) => wrapper.getComponent(SelectStub);

beforeEach(() => {
  mocks.options.value = [
    { organizationId: "org-1", organizationName: "Lakeview Outfitters", location: "", imageUrl: "" },
    { organizationId: "org-2", organizationName: "Cabin Co.", location: "", imageUrl: "" },
  ];
  mocks.failed.value = false;
});

describe("SalesRepTaskCustomerSelect", () => {
  it("offers the rep's customers", () => {
    const wrapper = createWrapper({ modelValue: "org-1" });

    expect(select(wrapper).props("items")).toEqual(mocks.options.value);
    expect(select(wrapper).props("modelValue")).toBe("org-1");
  });

  // The options stop at a cap, so a linked customer past it would otherwise vanish from the form on open.
  it("keeps the linked customer selectable when the options do not list it", () => {
    const wrapper = createWrapper({ modelValue: "org-9", name: "Far Away Co" });

    expect(select(wrapper).props("items")[0]).toEqual({ organizationId: "org-9", organizationName: "Far Away Co" });
  });

  // VcSelect counts "" as a selection, which would also offer a Clear with nothing to clear.
  it("hands VcSelect no selection when there is no customer", () => {
    const wrapper = createWrapper({ modelValue: "" });

    expect(select(wrapper).props("modelValue")).toBeUndefined();
    expect(select(wrapper).props("clearable")).toBe(false);
  });

  it("reports a cleared customer as none", () => {
    const wrapper = createWrapper({ modelValue: "org-1" });

    select(wrapper).vm.$emit("update:modelValue", undefined);

    expect(wrapper.emitted("update:modelValue")).toEqual([[""]]);
  });

  // An empty list on a failed fetch would read as "this rep serves nobody".
  it("says so when the customers could not be loaded", () => {
    mocks.failed.value = true;
    const wrapper = createWrapper({ modelValue: "" });

    expect(select(wrapper).props("error")).toBe(true);
    expect(select(wrapper).props("message")).toBe("sales_rep.tasks.form.customer_error");
  });
});
