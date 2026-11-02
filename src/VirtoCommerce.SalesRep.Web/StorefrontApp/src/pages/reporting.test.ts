import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import Reporting from "./reporting.vue";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    period: ref("q1"),
    customerId: ref("org-1"),
    customersLoading: ref(false),
    customersFailed: ref(false),
  };
});

// `t` prints its params after the key, so a spec sees which message was picked and what it was given.
vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
    n: (value: number) => String(value),
  }),
}));

vi.mock("../composables/useSalesRepCustomerOptions", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepCustomerOptions: () => ({
      options: ref([]),
      totalCount: ref(250),
      loading: state.customersLoading,
      failed: state.customersFailed,
      findOption: () => undefined,
    }),
  };
});

vi.mock("../composables/useReportingQueryParams", () => ({
  useReportingQueryParams: () => ({ period: state.period, customerId: state.customerId }),
}));

function mountPage() {
  return mount(Reporting, {
    global: {
      stubs: {
        VcWidget: { template: "<div><slot name='header' /><slot /></div>" },
        VcSelect: {
          props: ["items"],
          template: "<ul><li v-for='item in items' class='customer-option'>{{ item.organizationName }}</li></ul>",
        },
        ReportingPanel: {
          name: "ReportingPanel",
          props: ["organizationId", "period"],
          emits: ["update:period"],
          template:
            "<div class='panel-stub' :data-organization-id='organizationId' :data-period='period'><slot name='actions' /></div>",
        },
        VcTypography: true,
        VcLink: true,
        VcIcon: true,
      },
    },
  });
}

beforeEach(() => {
  state.period.value = "q1";
  state.customerId.value = "org-1";
  state.customersLoading.value = false;
  state.customersFailed.value = false;
});

describe("Reporting page", () => {
  describe("the All my customers option", () => {
    it("counts every served customer once the list has loaded", () => {
      expect(mountPage().find(".customer-option").text()).toBe('sales_rep.reporting.all_customers {"count":"250"}');
    });

    it.each([
      ["loading", state.customersLoading],
      ["failed", state.customersFailed],
    ])("carries no count while the list is %s", (_name, flag) => {
      flag.value = true;

      expect(mountPage().find(".customer-option").text()).toBe("sales_rep.reporting.all_customers_plain");
    });
  });

  it("reports on the period and customer from the URL", () => {
    const panel = mountPage().find(".panel-stub");

    expect(panel.attributes("data-period")).toBe("q1");
    expect(panel.attributes("data-organization-id")).toBe("org-1");
  });

  it("writes a period picked in the report back to the URL", async () => {
    const wrapper = mountPage();

    await wrapper.findComponent({ name: "ReportingPanel" }).vm.$emit("update:period", "12m");

    expect(state.period.value).toBe("12m");
  });

  it("puts the customer picker in the report's toolbar", () => {
    expect(mountPage().find(".panel-stub .customer-option").exists()).toBe(true);
  });
});
