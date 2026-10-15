import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import TimeSelect from "./time-select.vue";

// Writes the time the way a 24-hour locale would, so a label is easy to assert.
vi.mock("vue-i18n", () => ({
  useI18n: () => ({ d: (date: Date) => `${date.getHours()}h${String(date.getMinutes()).padStart(2, "0")}` }),
}));

const SelectStub = {
  props: ["modelValue", "items", "label", "required", "disabled"],
  emits: ["update:modelValue"],
  template: '<div class="select" />',
};

type ItemType = { value: string; label: string };

function createWrapper(props: { modelValue: string; stepMinutes?: number }) {
  return mount(TimeSelect, { props, global: { stubs: { VcSelect: SelectStub } } });
}

const items = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.getComponent(SelectStub).props("items") as ItemType[];

describe("TimeSelect", () => {
  it("offers the whole day in steps, labelled the way the locale writes a time", () => {
    const wrapper = createWrapper({ modelValue: "09:00", stepMinutes: 15 });

    expect(items(wrapper)).toHaveLength(96);
    expect(items(wrapper)[36]).toEqual({ value: "09:00", label: "9h00" });
  });

  // A call scheduled in the admin task UI can sit between two steps; opening the form must not move it.
  it("lists a time off the grid in its place", () => {
    const wrapper = createWrapper({ modelValue: "09:07", stepMinutes: 15 });

    const values = items(wrapper).map((item) => item.value);
    expect(values).toHaveLength(97);
    expect(values.slice(36, 39)).toEqual(["09:00", "09:07", "09:15"]);
  });

  it("hands the picked time back", async () => {
    const wrapper = createWrapper({ modelValue: "09:00" });

    wrapper.getComponent(SelectStub).vm.$emit("update:modelValue", "10:30");

    expect(wrapper.emitted("update:modelValue")).toEqual([["10:30"]]);
  });
});
