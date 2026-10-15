import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CustomerProfileActions from "./customer-profile-actions.vue";
import SalesRepTaskModal from "./sales-rep-task-modal.vue";

const mocks = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    openModal: vi.fn(),
    customer: ref<{ organizationName: string } | undefined>(undefined),
    tasksEnabled: true,
  };
});

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useModal: () => ({ openModal: mocks.openModal }),
}));
vi.mock("../composables/useSalesRepCustomer", () => ({
  useSalesRepCustomer: () => ({ customer: mocks.customer }),
}));
vi.mock("../composables/useSalesRepsConfig", () => ({
  isSalesRepTasksEnabled: () => mocks.tasksEnabled,
}));

function createWrapper() {
  return mount(CustomerProfileActions, {
    props: { organizationId: "org-1" },
    global: {
      stubs: {
        LayoutWidget: { template: '<div><slot name="default-container" /></div>' },
        VcIcon: true,
      },
    },
  });
}

function scheduleCallButton(wrapper: ReturnType<typeof createWrapper>) {
  const found = wrapper
    .findAll("button")
    .find((candidate) => candidate.text().includes("sales_rep.communication.quick_actions.schedule_call"));
  if (!found) {
    throw new Error("No Schedule call button");
  }
  return found;
}

beforeEach(() => {
  mocks.openModal.mockClear();
  mocks.customer.value = { organizationName: "Lakeview Outfitters" };
  mocks.tasksEnabled = true;
});

describe("CustomerProfileActions Schedule call", () => {
  it("opens a new call for this customer", async () => {
    const wrapper = createWrapper();

    await scheduleCallButton(wrapper).trigger("click");

    expect(mocks.openModal).toHaveBeenCalledWith({
      component: SalesRepTaskModal,
      props: { call: { customerId: "org-1", customerName: "Lakeview Outfitters" } },
    });
  });

  // A call is a task, and tasks live in the optional task-management module.
  it("stays unavailable without tasks", () => {
    mocks.tasksEnabled = false;
    const wrapper = createWrapper();

    expect(scheduleCallButton(wrapper).attributes("disabled")).toBeDefined();
  });

  it("waits for the customer, whose name the call carries", () => {
    mocks.customer.value = undefined;
    const wrapper = createWrapper();

    expect(scheduleCallButton(wrapper).attributes("disabled")).toBeDefined();
  });
});
