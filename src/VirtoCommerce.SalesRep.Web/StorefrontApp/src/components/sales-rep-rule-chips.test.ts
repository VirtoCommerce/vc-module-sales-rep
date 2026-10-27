import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { h } from "vue";
import { VcTabSwitch } from "@vc-frontend/core";
import SalesRepRuleChips from "./sales-rep-rule-chips.vue";
import type { SalesRepRuleType } from "../types";
import type { VNode } from "vue";

// formatStatCount formats in the store's culture.
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { cultureName: "en-US" },
}));

const RULES: SalesRepRuleType[] = [
  { name: "New", label: "New" },
  { name: "Processing", label: "Processing" },
];

// The real VcTabSwitch, not a stub: the selected state lives in its markup, so a stub cannot see it.
// VcIcon is resolved above the `v-if` that guards it, so it needs a stub even with no icon prop.
function mountChips(
  props: Partial<InstanceType<typeof SalesRepRuleChips>["$props"]> = {},
  slots?: { suffix: (params: { ruleName?: string }) => VNode },
) {
  return mount(SalesRepRuleChips, {
    props: { rules: RULES, allLabel: "All", ...props },
    slots,
    global: {
      components: { VcTabSwitch },
      stubs: {
        VcIcon: true,
        // The trigger and the content, side by side: which tabs a tooltip wraps is what the hint tests read.
        VcTooltip: {
          template:
            '<div class="tooltip-stub"><slot name="trigger" /><p class="tooltip-content"><slot name="content" /></p></div>',
        },
      },
    },
  });
}

const pressed = (wrapper: ReturnType<typeof mountChips>) =>
  wrapper.findAll(".sales-rep-rule-chip button").map((tab) => tab.attributes("aria-pressed"));

const labels = (wrapper: ReturnType<typeof mountChips>) =>
  wrapper.findAll(".sales-rep-rule-chip__label").map((label) => label.text());

describe("SalesRepRuleChips", () => {
  it("presses the baseline tab while no rule is chosen", () => {
    expect(pressed(mountChips())).toEqual(["true", "false", "false"]);
  });

  it("presses only the chosen rule's tab", () => {
    expect(pressed(mountChips({ modelValue: "Processing" }))).toEqual(["false", "false", "true"]);
  });

  it("clears the filter when the baseline tab is clicked back", async () => {
    const wrapper = mountChips({ modelValue: "Processing" });

    await wrapper.findAll(".sales-rep-rule-chip button")[0].trigger("click");

    expect(wrapper.emitted("update:modelValue")).toEqual([[undefined]]);
  });

  it("reports the rule name when a rule tab is clicked", async () => {
    const wrapper = mountChips();

    await wrapper.findAll(".sales-rep-rule-chip button")[2].trigger("click");

    expect(wrapper.emitted("update:modelValue")).toEqual([["Processing"]]);
  });

  // A set of alternatives reads with the widest option first — "All, Orders, Customers...".
  it("puts the baseline tab first", () => {
    expect(labels(mountChips())).toEqual(["All", "New", "Processing"]);
  });

  // Adornments are the caller's to decide: the component hands each tab's name to the slot and knows
  // nothing about what the name means. The baseline has no name.
  it("renders the suffix slot for every tab, naming each one", () => {
    const wrapper = mountChips(
      {},
      { suffix: (params: { ruleName?: string }) => h("i", { class: "mark" }, params.ruleName ?? "baseline") },
    );

    expect(wrapper.findAll(".mark").map((mark) => mark.text())).toEqual(["baseline", "New", "Processing"]);
  });

  // A hint opens on the whole tab: the tooltip wraps the tab, so nothing focusable has to sit inside its button.
  it("makes a hinted tab, and only that one, its tooltip's trigger", () => {
    const wrapper = mountChips({
      rules: [{ name: "New", label: "New", hint: "New hint" }, RULES[1]],
      allHint: "All hint",
    });

    const tooltips = wrapper.findAll(".tooltip-stub");
    expect(tooltips.map((tooltip) => tooltip.get(".sales-rep-rule-chip__label").text())).toEqual(["All", "New"]);
    expect(tooltips.map((tooltip) => tooltip.get(".tooltip-content").text())).toEqual(["All hint", "New hint"]);
    expect(labels(wrapper)).toEqual(["All", "New", "Processing"]);
  });

  // The baseline's value is a boolean, so no non-empty rule name can equal it — a string sentinel
  // made a rule of that name permanently unselectable and left two tabs pressed at once.
  it("keeps a rule selectable whatever non-empty name it has", async () => {
    const wrapper = mountChips({ rules: [{ name: "__all__", label: "Archive" }] });

    expect(pressed(wrapper)).toEqual(["true", "false"]);

    await wrapper.findAll(".sales-rep-rule-chip button")[1].trigger("click");

    expect(wrapper.emitted("update:modelValue")).toEqual([["__all__"]]);
  });

  // The backend "all" passthrough would duplicate the baseline tab.
  it("drops a backend all-rule instead of rendering a second baseline", () => {
    const wrapper = mountChips({ rules: [{ name: "All", label: "All" }, ...RULES] });

    expect(wrapper.findAll(".sales-rep-rule-chip")).toHaveLength(3);
  });

  it("falls back to the baseline when the chosen rule leaves the vocabulary", async () => {
    const wrapper = mountChips({ modelValue: "Processing" });

    await wrapper.setProps({ rules: [{ name: "New", label: "New" }] });

    expect(wrapper.emitted("update:modelValue")).toEqual([[undefined]]);
  });

  // An in-flight refetch empties the list transiently; that must not read as "the rule is gone".
  it("keeps the selection while the vocabulary is still loading", async () => {
    const wrapper = mountChips({ modelValue: "Processing", loading: true });

    await wrapper.setProps({ rules: [] });

    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
  });

  it("renders a count only for the rules that carry one", () => {
    const wrapper = mountChips({
      rules: [
        { name: "New", label: "New", count: 4 },
        { name: "Processing", label: "Processing" },
      ],
      allCount: 9,
    });

    expect(wrapper.findAll(".sales-rep-rule-chip__count").map((count) => count.text())).toEqual(["9", "4"]);
  });

  // A zero count is on the documents page's first-paint path: allCount starts at 0 and the category
  // query resolves before the documents one, so a truthiness guard would hide a real number.
  it("renders a zero count rather than dropping it", () => {
    const wrapper = mountChips({ rules: [{ name: "New", label: "New", count: 0 }], allCount: 0 });

    expect(wrapper.findAll(".sales-rep-rule-chip__count").map((count) => count.text())).toEqual(["0", "0"]);
  });
});
