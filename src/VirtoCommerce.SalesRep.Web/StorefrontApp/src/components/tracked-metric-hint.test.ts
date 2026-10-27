import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import TrackedMetricHint from "./tracked-metric-hint.vue";

const createWrapper = createWrapperFactory(mount, TrackedMetricHint, {
  global: {
    stubs: {
      // The trigger props the kit hands its default slot, and the content beside it.
      VcTooltip: {
        name: "VcTooltipStub",
        template:
          '<div><slot :trigger-props="{ \'aria-describedby\': \'hint\' }" /><p class="tooltip-content"><slot name="content" /></p></div>',
      },
      VcIcon: { props: ["name", "label"], template: '<i :data-name="name" :aria-label="label" />' },
    },
  },
});

describe("TrackedMetricHint", () => {
  // On its own (the profile's definition rows) the hint needs a trigger the keyboard can reach.
  it("has a focusable trigger of its own", () => {
    const wrapper = createWrapper();
    const trigger = wrapper.get('[tabindex="0"]');

    expect(trigger.attributes("aria-describedby")).toBe("hint");
    expect(wrapper.get(".tooltip-content").text()).toBe("sales_rep.activity.tracked_hint");
  });

  // Inside a tab the tab is the trigger: anything focusable here would sit inside its button.
  it("is the labelled icon alone, with nothing focusable, inside a tab", () => {
    const wrapper = createWrapper({ props: { iconOnly: true } });

    expect(wrapper.find("[tabindex]").exists()).toBe(false);
    expect(wrapper.findComponent({ name: "VcTooltipStub" }).exists()).toBe(false);
    expect(wrapper.get('[data-name="hourglass"]').attributes("aria-label")).toBe("sales_rep.activity.tracked_hint");
  });
});
