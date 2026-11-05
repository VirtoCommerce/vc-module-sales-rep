/* The ui-kit stubs below are minimal test doubles, not shippable components. */

import { flushPromises, mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { VcSelectMultiple } from "../mocks/vc-select-multiple";
import CustomerCommunicationModal from "./customer-communication-modal.vue";
import type { OrganizationRowType } from "../types";
import { VcButton } from "@vc-frontend/core";

const KEY = "sales_rep.communication";

const mocks = await vi.hoisted(async () => {
  const { ref } = await import("vue");

  return {
    sendCommunication: vi.fn(),
    sending: ref(false),
    options: ref<OrganizationRowType[]>([]),
    optionsLoading: ref(false),
    failed: ref(false),
    notifications: { success: vi.fn(), warning: vi.fn(), error: vi.fn() },
    close: vi.fn(),
  };
});

vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, named?: Record<string, unknown>) => (named ? `${key}|${JSON.stringify(named)}` : key),
    te: (key: string) => key === "sales_rep.communication.warnings.EmailNoRecipients",
  }),
}));

vi.mock("../composables/useSalesRepCommunication", () => ({
  useSalesRepCommunication: () => ({ sendCommunication: mocks.sendCommunication, loading: mocks.sending }),
}));

vi.mock("../composables/useSalesRepCustomerOptions", () => ({
  useSalesRepCustomerOptions: () => ({
    options: mocks.options,
    loading: mocks.optionsLoading,
    failed: mocks.failed,
    findOption: (organizationId: string) =>
      mocks.options.value.find((option) => option.organizationId === organizationId),
  }),
}));

vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useNotifications: () => mocks.notifications,
}));

// Renders both slots and exposes the close() the popup calls through its ref.
const VcModal = {
  props: ["title"],
  methods: { close: mocks.close },
  template: '<div class="modal"><slot /><slot name="actions" :close="close" /></div>',
};

// One stub for the text fields: the label tells them apart.
const FieldStub = {
  props: ["modelValue", "label"],
  emits: ["update:modelValue"],
  template:
    '<input class="field" :data-label="label" :value="modelValue"' +
    " @input=\"$emit('update:modelValue', $event.target.value)\" />",
};

const CheckboxStub = {
  props: ["modelValue"],
  emits: ["update:modelValue"],
  template:
    '<label class="checkbox"><input type="checkbox" :checked="modelValue"' +
    " @change=\"$emit('update:modelValue', $event.target.checked)\" /><slot /></label>",
};

const SUCCESS = { succeeded: true, pushSent: true, emailSent: true, warnings: [] as string[] };

// The customer a row or a profile opens the popup for.
const ACME = { organizationId: "org-1", organizationName: "Acme Inc." };

function customer(organizationId: string, organizationName: string, location = ""): OrganizationRowType {
  return { organizationId, organizationName, location, imageUrl: "" };
}

function createWrapper(props: Record<string, unknown> = {}, attachTo?: HTMLElement) {
  return mount(CustomerCommunicationModal, {
    props,
    attachTo,
    global: {
      components: { VcSelect: VcSelectMultiple, VcButton },
      stubs: {
        VcModal,
        VcInput: FieldStub,
        VcTextarea: FieldStub,
        VcCheckbox: CheckboxStub,
        VcLabel: { template: "<span><slot /></span>" },
        VcImage: true,
        VcIcon: true,
      },
    },
  });
}

type WrapperType = ReturnType<typeof createWrapper>;

const picker = (wrapper: WrapperType) => wrapper.find("[data-test-id='customer-communication-customers']");
const recipientNames = (wrapper: WrapperType) =>
  wrapper.findAll("[data-test-id='wishlist-sharing-recipient-row']").map((row) => row.text());
const fieldMessage = (wrapper: WrapperType) => wrapper.find("[data-test-id='field-message']");

function sendButton(wrapper: WrapperType) {
  const found = wrapper.findAll("button").find((button) => button.text() === `${KEY}.send_button`);
  if (!found) {
    throw new Error("No Send button");
  }
  return found;
}

// The rest of a sendable message: some text and a channel.
async function fillMessage(wrapper: WrapperType) {
  await wrapper.find(`[data-label='${KEY}.message_label']`).setValue("Spring prices are out");
  await wrapper.find("label.checkbox input").setValue(true);
  await flushPromises();
}

async function send(wrapper: WrapperType) {
  await sendButton(wrapper).trigger("click");
  await flushPromises();
}

async function pick(wrapper: WrapperType, organizationId: string) {
  await wrapper.find(`[data-test-id='customer-option-${organizationId}']`).trigger("click");
  await flushPromises();
}

async function removeRecipient(wrapper: WrapperType, organizationId: string) {
  await wrapper.find(`[data-test-id='wishlist-sharing-remove-recipient-${organizationId}']`).trigger("click");
  await flushPromises();
}

beforeEach(() => {
  mocks.sendCommunication.mockReset().mockResolvedValue(SUCCESS);
  mocks.sending.value = false;
  mocks.options.value = [
    customer("org-1", "Acme Inc.", "Richmond, Virginia"),
    customer("org-2", "Globex"),
    customer("org-3", "Initech"),
  ];
  mocks.optionsLoading.value = false;
  mocks.failed.value = false;
  mocks.close.mockReset();
  Object.values(mocks.notifications).forEach((spy) => spy.mockReset());
});

describe("CustomerCommunicationModal", () => {
  it("asks for the customers first, then the title, the message and the channels", () => {
    const wrapper = createWrapper();

    const fields = wrapper.findAll(
      "[data-test-id='customer-communication-customers'], .field, .customer-communication-modal__channels",
    );

    expect(
      fields.map((field) => field.attributes("data-test-id") ?? field.attributes("data-label") ?? "channels"),
    ).toEqual(["customer-communication-customers", `${KEY}.title_label`, `${KEY}.message_label`, "channels"]);
  });

  describe("opened from the list's own Send message button", () => {
    it("starts with nobody picked, holds Send back, and does not scold the rep for it yet", async () => {
      const wrapper = createWrapper();
      await fillMessage(wrapper);

      expect(recipientNames(wrapper)).toEqual([]);
      expect(sendButton(wrapper).attributes("disabled")).toBeDefined();
      expect(fieldMessage(wrapper).exists()).toBe(false);
    });

    it("sends to the customers the rep picks", async () => {
      const wrapper = createWrapper();
      await pick(wrapper, "org-2");
      await pick(wrapper, "org-3");
      await fillMessage(wrapper);

      await send(wrapper);

      expect(mocks.sendCommunication).toHaveBeenCalledWith(
        expect.objectContaining({ organizationIds: ["org-2", "org-3"], sendEmail: true, sendPush: false }),
      );
    });
  });

  describe("opened from a customer row", () => {
    it("starts with that customer picked, as the options describe it", () => {
      const wrapper = createWrapper(ACME);

      expect(recipientNames(wrapper)).toEqual([expect.stringContaining("Acme Inc.")]);
      expect(recipientNames(wrapper)[0]).toContain("Richmond, Virginia");
      expect(wrapper.find("[data-test-id='customer-option-org-1']").attributes("aria-pressed")).toBe("true");
    });

    it("takes more customers, and sends to all of them at once", async () => {
      const wrapper = createWrapper(ACME);
      await pick(wrapper, "org-3");
      await fillMessage(wrapper);

      await send(wrapper);

      expect(mocks.sendCommunication).toHaveBeenCalledTimes(1);
      expect(mocks.sendCommunication.mock.calls[0][0].organizationIds).toEqual(["org-1", "org-3"]);
    });

    it("holds Send back and says why once every customer is unpicked", async () => {
      const wrapper = createWrapper(ACME);
      await fillMessage(wrapper);
      expect(sendButton(wrapper).attributes("disabled")).toBeUndefined();

      await removeRecipient(wrapper, "org-1");

      expect(sendButton(wrapper).attributes("disabled")).toBeDefined();
      expect(fieldMessage(wrapper).text()).toBe(`${KEY}.customers_required`);
      expect(picker(wrapper).attributes("aria-invalid")).toBe("true");
    });

    it("lets the rep pick again after emptying the field from the picker itself", async () => {
      const wrapper = createWrapper(ACME);
      await fillMessage(wrapper);

      await wrapper.find("[data-test-id='customer-select-clear']").trigger("click");
      await flushPromises();
      expect(fieldMessage(wrapper).text()).toBe(`${KEY}.customers_required`);

      await pick(wrapper, "org-2");

      expect(fieldMessage(wrapper).exists()).toBe(false);
      expect(sendButton(wrapper).attributes("disabled")).toBeUndefined();
    });

    // The recipients list unmounts once it empties, and the focus must not go with it to the page.
    describe("when the recipients list empties", () => {
      const customersField = (wrapper: WrapperType) => picker(wrapper).find(".vc-select__container").element;

      it("moves the focus to the Customers field after Clear all", async () => {
        const wrapper = createWrapper(ACME, document.body);
        await pick(wrapper, "org-2");

        await wrapper.find("[data-test-id='wishlist-sharing-clear-recipients-button']").trigger("click");
        await flushPromises();

        expect(document.activeElement).toBe(customersField(wrapper));
        wrapper.unmount();
      });

      it("moves the focus to the Customers field when the last row is removed", async () => {
        const wrapper = createWrapper(ACME, document.body);

        await removeRecipient(wrapper, "org-1");

        expect(document.activeElement).toBe(customersField(wrapper));
        wrapper.unmount();
      });

      // The list hands the focus to the next row itself; the field must not take it, not even for a moment.
      it("leaves the focus in the list while rows remain", async () => {
        const wrapper = createWrapper(ACME, document.body);
        await pick(wrapper, "org-2");
        const fieldFocused = vi.fn();
        customersField(wrapper).addEventListener("focus", fieldFocused);

        await removeRecipient(wrapper, "org-1");

        expect(fieldFocused).not.toHaveBeenCalled();
        expect(document.activeElement).toBe(
          wrapper.find("[data-test-id='wishlist-sharing-remove-recipient-org-2']").element,
        );
        wrapper.unmount();
      });
    });

    // The options stop at a cap; the row's customer may sit past it and must stay pickable and unpickable.
    it("lists the customer it opened for even when the options stop short of it", () => {
      const wrapper = createWrapper({ organizationId: "org-9", organizationName: "Zenith Ltd." });

      expect(recipientNames(wrapper)).toEqual([expect.stringContaining("Zenith Ltd.")]);
      expect(wrapper.find("[data-test-id='customer-option-org-9']").attributes("aria-pressed")).toBe("true");
    });
  });

  describe("opened from Quick actions on a customer's profile", () => {
    it("shows that customer only, with no way to change who receives it", () => {
      const wrapper = createWrapper({ ...ACME, locked: true });

      expect(picker(wrapper).exists()).toBe(false);
      expect(recipientNames(wrapper)).toEqual([expect.stringContaining("Acme Inc.")]);
      expect(wrapper.find("[data-test-id='wishlist-sharing-remove-recipient-org-1']").exists()).toBe(false);
      expect(wrapper.find("[data-test-id='wishlist-sharing-clear-recipients-button']").exists()).toBe(false);
      expect(wrapper.text()).toContain(`${KEY}.subtitle|${JSON.stringify({ organization: "Acme Inc." })}`);
    });

    it("sends to that customer", async () => {
      const wrapper = createWrapper({ ...ACME, locked: true });
      await fillMessage(wrapper);

      await send(wrapper);

      expect(mocks.sendCommunication.mock.calls[0][0].organizationIds).toEqual(["org-1"]);
    });
  });

  it("says when the customer list could not be loaded", () => {
    mocks.failed.value = true;

    const wrapper = createWrapper();

    expect(fieldMessage(wrapper).text()).toBe(`${KEY}.customers_error`);
  });

  it("stays open and says so when nothing was sent", async () => {
    mocks.sendCommunication.mockResolvedValue({ ...SUCCESS, succeeded: false, emailSent: false, pushSent: false });
    const wrapper = createWrapper(ACME);
    await fillMessage(wrapper);

    await send(wrapper);

    expect(mocks.close).not.toHaveBeenCalled();
    expect(mocks.notifications.error).toHaveBeenCalledWith(expect.objectContaining({ text: `${KEY}.error_text` }));
    expect(mocks.notifications.success).not.toHaveBeenCalled();
  });

  it("closes and names what did not go out on a partial send", async () => {
    mocks.sendCommunication.mockResolvedValue({ ...SUCCESS, emailSent: false, warnings: ["EmailNoRecipients"] });
    const wrapper = createWrapper(ACME);
    await fillMessage(wrapper);

    await send(wrapper);

    expect(mocks.close).toHaveBeenCalled();
    expect(mocks.notifications.warning).toHaveBeenCalledWith(
      expect.objectContaining({ text: `${KEY}.partial_success_text ${KEY}.warnings.EmailNoRecipients` }),
    );
  });
});
