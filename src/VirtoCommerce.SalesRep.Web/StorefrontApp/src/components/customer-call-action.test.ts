import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CustomerCallAction from "./customer-call-action.vue";
import type { CustomerPhoneType } from "../phones";

vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
  }),
}));

const jane: CustomerPhoneType = { name: "Jane Smith", number: "+1 555 010 2030", href: "tel:+15550102030" };
const office: CustomerPhoneType = { name: "Acme Corp", number: "555 000", href: "tel:555000" };

const closeMenu = vi.fn();

function mountAction(phones: CustomerPhoneType[], variant?: "icon" | "button") {
  return mount(CustomerCallAction, {
    props: { phones, variant },
    global: {
      stubs: {
        VcTooltip: {
          props: ["disabled"],
          template:
            "<div class='tooltip' :data-disabled='String(disabled)'><slot name='trigger' /><span class='tooltip-text'><slot name='content' /></span></div>",
        },
        VcPopover: {
          name: "VcPopover",
          emits: ["toggle"],
          setup: () => ({ closeMenu }),
          template:
            "<div class='popover'><slot :trigger-props=\"{ 'aria-expanded': false }\" /><div class='menu' role='dialog' tabindex='-1'><slot name='content' :close='closeMenu' /></div></div>",
        },
        VcButton: {
          props: ["externalLink", "disabled", "icon", "prependIcon"],
          template:
            "<button class='button' :data-href='externalLink' :data-icon='icon' :data-prepend-icon='prependIcon' :disabled='disabled'><slot /></button>",
        },
        VcMenuItem: {
          props: ["externalLink"],
          template: "<a class='menu-item' :href='externalLink'><slot /></a>",
        },
      },
    },
  });
}

const tooltipOf = (wrapper: ReturnType<typeof mountAction>) => ({
  text: wrapper.find(".tooltip-text").text(),
  disabled: wrapper.find(".tooltip").attributes("data-disabled") === "true",
});

describe("CustomerCallAction", () => {
  it("calls the only number directly, under the Call customer tooltip", () => {
    const wrapper = mountAction([jane]);
    const button = wrapper.find(".button");

    expect(button.attributes("data-href")).toBe("tel:+15550102030");
    expect(button.attributes("aria-label")).toBe(
      'sales_rep.communication.call.action_with_number {"number":"+1 555 010 2030"}',
    );
    expect(wrapper.find(".popover").exists()).toBe(false);
    expect(tooltipOf(wrapper)).toEqual({ text: "sales_rep.communication.call.action", disabled: false });
  });

  it("offers every number, with whose it is, when there are several", () => {
    const wrapper = mountAction([jane, office]);
    const items = wrapper.findAll(".menu-item");

    expect(items.map((item) => item.attributes("href"))).toEqual(["tel:+15550102030", "tel:555000"]);
    expect(items[0].text()).toContain("Jane Smith");
    expect(items[0].text()).toContain("+1 555 010 2030");
    expect(wrapper.find(".popover .button").attributes("aria-expanded")).toBe("false");
    expect(tooltipOf(wrapper).text).toBe("sales_rep.communication.call.action");
  });

  it("hides the tooltip while the number list is open", async () => {
    const wrapper = mountAction([jane, office]);

    await wrapper.findComponent({ name: "VcPopover" }).vm.$emit("toggle", true);

    expect(tooltipOf(wrapper).disabled).toBe(true);
  });

  it("is disabled with No phone number when the customer has none, and still takes focus to say so", () => {
    const wrapper = mountAction([]);
    const reason = wrapper.find(".customer-call-action__disabled");

    expect(wrapper.find(".button").attributes("disabled")).toBeDefined();
    expect(tooltipOf(wrapper)).toEqual({ text: "sales_rep.communication.call.no_phone", disabled: false });
    expect(reason.attributes("tabindex")).toBe("0");
    expect(reason.attributes("aria-disabled")).toBe("true");
    expect(reason.attributes("aria-label")).toBe(
      "sales_rep.communication.call.action, sales_rep.communication.call.no_phone",
    );
  });

  it("is an icon in the table and a labelled button on the mobile cards", () => {
    expect(mountAction([jane]).find(".button").attributes("data-icon")).toBe("phone");

    const button = mountAction([jane], "button").find(".button");

    expect(button.attributes("data-prepend-icon")).toBe("phone");
    expect(button.text()).toBe("sales_rep.communication.call.action");
  });

  it("drops the tooltip that would repeat the labelled button, and keeps it to say there is no number", () => {
    expect(tooltipOf(mountAction([jane], "button")).disabled).toBe(true);
    expect(tooltipOf(mountAction([jane, office], "button")).disabled).toBe(true);
    expect(tooltipOf(mountAction([], "button"))).toEqual({
      text: "sales_rep.communication.call.no_phone",
      disabled: false,
    });
  });

  describe("focus leaving the number list", () => {
    beforeEach(() => closeMenu.mockClear());

    function leaveFocus(wrapper: ReturnType<typeof mountAction>, relatedTarget: EventTarget | null) {
      wrapper.find(".menu-item").element.dispatchEvent(new FocusEvent("focusout", { bubbles: true, relatedTarget }));
    }

    it("closes the list when Tab moves focus past the last number", () => {
      const wrapper = mountAction([jane, office]);
      const nextRowLink = document.createElement("a");

      leaveFocus(wrapper, nextRowLink);

      expect(closeMenu).toHaveBeenCalledOnce();
    });

    it("keeps the list open while focus moves between numbers or onto the panel", () => {
      const wrapper = mountAction([jane, office]);

      leaveFocus(wrapper, wrapper.findAll(".menu-item")[1].element);
      leaveFocus(wrapper, wrapper.find(".menu").element);

      expect(closeMenu).not.toHaveBeenCalled();
    });

    it("keeps the list open when focus leaves the window", () => {
      leaveFocus(mountAction([jane, office]), null);

      expect(closeMenu).not.toHaveBeenCalled();
    });
  });
});
