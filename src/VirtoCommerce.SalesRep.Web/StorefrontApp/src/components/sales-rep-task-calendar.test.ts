import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import SalesRepTaskCalendar from "./sales-rep-task-calendar.vue";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));

const CalendarStub = {
  name: "VcCalendar",
  props: { modelValue: String, month: String, showFooter: Boolean },
  emits: ["update:modelValue", "update:month"],
  template: '<div class="calendar" />',
};

function createWrapper(props: Record<string, unknown> = {}) {
  return mount(SalesRepTaskCalendar, {
    props: { modelValue: "2026-10-15", ...props },
    global: { stubs: { VcCalendar: CalendarStub } },
  });
}

describe("SalesRepTaskCalendar", () => {
  it("reports the day picked", () => {
    const wrapper = createWrapper();

    wrapper.getComponent(CalendarStub).vm.$emit("update:modelValue", "2026-10-20");

    expect(wrapper.emitted("update:modelValue")).toEqual([["2026-10-20"]]);
  });

  // The dashboard widget always lists some day, so a clear there would leave it nothing to scope to.
  it("ignores a clear unless the day may be cleared", () => {
    const wrapper = createWrapper();

    wrapper.getComponent(CalendarStub).vm.$emit("update:modelValue", undefined);

    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    expect(wrapper.getComponent(CalendarStub).props("showFooter")).toBe(false);
  });

  // The Tasks page: Today and Clear in the footer, and a second click on the picked day, unpick it.
  it("reports a clear, and offers the footer, when the day may be cleared", () => {
    const wrapper = createWrapper({ clearable: true });

    wrapper.getComponent(CalendarStub).vm.$emit("update:modelValue", undefined);

    expect(wrapper.emitted("update:modelValue")).toEqual([[undefined]]);
    expect(wrapper.getComponent(CalendarStub).props("showFooter")).toBe(true);
  });
});
