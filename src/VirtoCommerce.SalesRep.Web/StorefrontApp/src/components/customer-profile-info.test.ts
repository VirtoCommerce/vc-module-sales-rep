import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CustomerProfileInfo from "./customer-profile-info.vue";
import type { CustomerPhoneType } from "../phones";
import type { SalesRepCustomerProfileType } from "../types/customer-profile";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return { customer: ref<SalesRepCustomerProfileType | undefined>(undefined) };
});

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("../composables/useSalesRepCustomer", async () => {
  const { ref } = await import("vue");
  return { useSalesRepCustomer: () => ({ customer: state.customer, loading: ref(false) }) };
});

const jane: CustomerPhoneType = { name: "Jane Smith", number: "+1 555 010 2030", href: "tel:+15550102030" };
const office: CustomerPhoneType = { name: "Acme Corp", number: "555 000", href: "tel:555000" };

function customer(phones: CustomerPhoneType[]): SalesRepCustomerProfileType {
  return {
    organizationId: "org-1",
    organizationName: "Acme Corp",
    iconUrl: "",
    accountType: "Garden Center",
    phones,
    shipTo: "",
    primaryContactName: "Jane Smith",
  };
}

function mountInfo() {
  return mount(CustomerProfileInfo, {
    props: { organizationId: "org-1" },
    global: {
      stubs: {
        LayoutWidget: { template: "<section><slot /></section>" },
        VcEmptyView: true,
        VcIcon: true,
        CustomerCallAction: { name: "CustomerCallAction", props: ["phones"], template: "<span class='call-action' />" },
      },
    },
  });
}

beforeEach(() => {
  state.customer.value = undefined;
});

describe("CustomerProfileInfo phone", () => {
  it("calls the default number from the Phone row", () => {
    state.customer.value = customer([jane]);
    const link = mountInfo().find(".customer-profile-info__link");

    expect(link.attributes("href")).toBe("tel:+15550102030");
    expect(link.text()).toBe("+1 555 010 2030");
  });

  it("offers the other numbers beside it when there are several", () => {
    state.customer.value = customer([jane, office]);
    const action = mountInfo().findComponent({ name: "CustomerCallAction" });

    expect(action.exists()).toBe(true);
    expect(action.props("phones")).toEqual([jane, office]);
  });

  it("keeps a single number to the link alone", () => {
    state.customer.value = customer([jane]);

    expect(mountInfo().find(".call-action").exists()).toBe(false);
  });

  it("shows no Phone row for a customer without a number", () => {
    state.customer.value = customer([]);
    const wrapper = mountInfo();

    expect(wrapper.text()).not.toContain("sales_rep.customer_profile.info.phone");
    expect(wrapper.text()).toContain("sales_rep.customer_profile.info.primary_contact");
  });
});
