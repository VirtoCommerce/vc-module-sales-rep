import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import SalesRepFilterChips from "./sales-rep-filter-chips.vue";
import type { SalesRepFilterChipType } from "../types";

vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
  }),
}));

// VcChip's own markup: the content is a button only when the chip is clickable, and the close button carries the
// kit's class, which the row finds the next stop by. The buttons also answer Enter: a browser turns it into a click,
// jsdom does not, and a dispatched click would read as the mouse.
const ChipStub = {
  props: { closable: Boolean, clickable: Boolean, truncate: Boolean, closeButtonAriaLabel: String },
  emits: ["close", "click"],
  template:
    '<span class="chip" :data-closable="String(!!closable)" :data-truncate="String(!!truncate)">' +
    "<component :is=\"clickable ? 'button' : 'span'\" @click=\"$emit('click')\" @keydown.enter=\"$emit('click')\">" +
    "<slot /></component>" +
    '<button v-if="closable" class="vc-chip__close-button" :aria-label="closeButtonAriaLabel"' +
    " @click.stop=\"$emit('close')\" @keydown.enter=\"$emit('close')\" />" +
    "</span>",
};

const CHIPS = [
  { id: "day", label: "Due today" },
  { id: "customerIds:org-1", label: "Cabin Co." },
];

const THREE_CHIPS = [...CHIPS, { id: "priorities:High", label: "High" }];

enableAutoUnmount(afterEach);

function createWrapper(chips = CHIPS) {
  return mount(SalesRepFilterChips, {
    props: { chips },
    global: { stubs: { VcChip: ChipStub, VcIcon: true } },
  });
}

/** In the document, so focus works, and answering like a page: a closed chip leaves, Reset clears the row. */
function createLiveWrapper(chips: SalesRepFilterChipType[] = THREE_CHIPS) {
  const fallbackFocus = vi.fn();
  const wrapper = mount(SalesRepFilterChips, {
    attachTo: document.body,
    props: {
      chips,
      fallbackFocus,
      onRemove: (closed: SalesRepFilterChipType) =>
        void wrapper.setProps({ chips: wrapper.props("chips").filter((chip) => chip.id !== closed.id) }),
      onReset: () => void wrapper.setProps({ chips: [] }),
    },
    global: { stubs: { VcChip: ChipStub, VcIcon: true } },
  });

  return { wrapper, fallbackFocus };
}

/** Presses a button from the keyboard: focused, as Tab leaves it, then Enter. */
async function press(button: HTMLElement): Promise<void> {
  button.focus();
  button.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
  await flushPromises();
}

function closeButtons(wrapper: ReturnType<typeof createWrapper>) {
  return wrapper.findAll<HTMLButtonElement>(".vc-chip__close-button");
}

function closeButtonLabel(label: string): string {
  return `sales_rep.filters.remove_filter ${JSON.stringify({ label })}`;
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

    expect(closeButtons(wrapper).map((button) => button.attributes("aria-label"))).toEqual([
      closeButtonLabel("Due today"),
      closeButtonLabel("Cabin Co."),
    ]);
  });

  // A customer's full name can be longer than the row: cut short, it still has to be readable somewhere.
  it("cuts a long label short and keeps all of it in the tooltip", () => {
    const wrapper = createWrapper();

    const chips = wrapper.findAll(".chip");
    expect(chips.map((chip) => chip.attributes("data-truncate"))).toEqual(["true", "true", "false"]);
    expect(chips.map((chip) => chip.attributes("title"))).toEqual(["Due today", "Cabin Co.", undefined]);
  });

  it("hands back the chip closed", async () => {
    const wrapper = createWrapper();

    await closeButtons(wrapper)[1].trigger("click");

    expect(wrapper.emitted("remove")).toEqual([[CHIPS[1]]]);
  });

  it("asks for a reset from the reset chip", async () => {
    const wrapper = createWrapper();

    await wrapper.findAll(".chip").at(-1)?.get("button").trigger("click");

    expect(wrapper.emitted("reset")).toHaveLength(1);
  });

  it("draws nothing, not even the reset chip, while nothing is filtered", () => {
    const wrapper = createWrapper([]);

    expect(wrapper.find(".chip").exists()).toBe(false);
  });
});

// The pressed button leaves with its chip. Without a next stop, focus would drop to <body>, and a keyboard user back
// to the top of the page.
describe("SalesRepFilterChips focus", () => {
  it("hands the focus to the chip that takes the closed one's place", async () => {
    const { wrapper } = createLiveWrapper();

    await press(closeButtons(wrapper)[1].element);

    expect(document.activeElement?.getAttribute("aria-label")).toBe(closeButtonLabel("High"));
  });

  it("hands the focus to the new last chip when the last one is closed", async () => {
    const { wrapper } = createLiveWrapper();

    await press(closeButtons(wrapper)[2].element);

    expect(document.activeElement?.getAttribute("aria-label")).toBe(closeButtonLabel("Cabin Co."));
  });

  it("hands the focus past the row once its last chip is closed", async () => {
    const { wrapper, fallbackFocus } = createLiveWrapper([CHIPS[0]]);

    await press(closeButtons(wrapper)[0].element);

    expect(wrapper.find(".chip").exists()).toBe(false);
    expect(fallbackFocus).toHaveBeenCalledOnce();
  });

  it("hands the focus past the row after a reset", async () => {
    const { wrapper, fallbackFocus } = createLiveWrapper();

    await press(wrapper.findAll(".chip").at(-1)!.get("button").element);

    expect(fallbackFocus).toHaveBeenCalledOnce();
  });

  // A click focuses the button without the ring, so nothing moves: a tap never opens a phone's keyboard.
  it("leaves the focus alone after a click", async () => {
    const { wrapper, fallbackFocus } = createLiveWrapper([CHIPS[0]]);
    const button = closeButtons(wrapper)[0].element;

    button.focus();
    button.click();
    await flushPromises();

    expect(fallbackFocus).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(document.body);
  });
});
