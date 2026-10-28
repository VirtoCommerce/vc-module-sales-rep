import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import SalesRepFilterChips from "./sales-rep-filter-chips.vue";

vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
  }),
}));

const ChipStub = {
  props: { closable: Boolean, clickable: Boolean, closeButtonAriaLabel: String },
  emits: ["close", "click"],
  template:
    '<span class="chip" :data-closable="String(!!closable)" @click="$emit(\'click\')"><slot />' +
    '<button v-if="closable" class="chip-close" :aria-label="closeButtonAriaLabel" @click.stop="$emit(\'close\')" />' +
    "</span>",
};

const CHIPS = [
  { id: "day", label: "Due today" },
  { id: "customerIds:org-1", label: "Cabin Co." },
];

function createWrapper(chips = CHIPS) {
  return mount(SalesRepFilterChips, {
    props: { chips },
    global: { stubs: { VcChip: ChipStub, VcIcon: true } },
  });
}

describe("SalesRepFilterChips", () => {
  it("draws a closable chip per filter, then the reset chip", () => {
    const wrapper = createWrapper();

    const chips = wrapper.findAll(".chip");
    expect(chips.map((chip) => chip.text())).toEqual(["Due today", "Cabin Co.", "common.buttons.reset_filters"]);
    expect(chips.map((chip) => chip.attributes("data-closable"))).toEqual(["true", "true", "false"]);
  });

  it("names every close button after the filter it removes", () => {
    const wrapper = createWrapper();

    expect(wrapper.findAll(".chip-close").map((button) => button.attributes("aria-label"))).toEqual([
      'sales_rep.filters.remove_filter {"label":"Due today"}',
      'sales_rep.filters.remove_filter {"label":"Cabin Co."}',
    ]);
  });

  it("hands back the chip closed", async () => {
    const wrapper = createWrapper();

    await wrapper.findAll(".chip-close")[1].trigger("click");

    expect(wrapper.emitted("remove")).toEqual([[CHIPS[1]]]);
  });

  it("asks for a reset from the reset chip", async () => {
    const wrapper = createWrapper();

    await wrapper.findAll(".chip").at(-1)?.trigger("click");

    expect(wrapper.emitted("reset")).toHaveLength(1);
  });

  it("draws nothing, not even the reset chip, while nothing is filtered", () => {
    const wrapper = createWrapper([]);

    expect(wrapper.find(".chip").exists()).toBe(false);
  });
});
