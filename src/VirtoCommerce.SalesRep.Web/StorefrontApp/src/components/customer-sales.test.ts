import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CUSTOMER_SALES_PERIOD_STORAGE_KEY, REPORTING_ROUTE_NAME } from "../constants";
import { REPORTING_DEFAULT_PERIOD } from "../reporting";
import CustomerSales from "./customer-sales.vue";

vi.mock("vue-i18n", () => ({
  useI18n: () => ({ t: (key: string) => key }),
}));

function mountBlock(organizationId = "org-1") {
  return mount(CustomerSales, {
    props: { title: "Sales statistics", organizationId },
    global: {
      stubs: {
        LayoutWidget: { props: ["title"], template: "<section><slot name='append' /><slot /></section>" },
        VcLink: { props: ["to"], template: "<a class='reporting-link' :data-to='JSON.stringify(to)'><slot /></a>" },
        VcIcon: true,
        ReportingPanel: {
          name: "ReportingPanel",
          props: { organizationId: String, period: String, compact: Boolean, withTable: Boolean },
          emits: ["update:period"],
          template: "<div class='panel-stub' />",
        },
      },
    },
  });
}

function panelOf(wrapper: ReturnType<typeof mountBlock>) {
  return wrapper.findComponent({ name: "ReportingPanel" });
}

function linkOf(wrapper: ReturnType<typeof mountBlock>) {
  return JSON.parse(wrapper.find(".reporting-link").attributes("data-to") ?? "{}") as unknown;
}

beforeEach(() => {
  localStorage.clear();
});

describe("CustomerSales", () => {
  it("reports on this customer, without the table, in the compact layout", () => {
    const panel = panelOf(mountBlock("org-1"));

    expect(panel.props("organizationId")).toBe("org-1");
    expect(panel.props("withTable")).toBe(false);
    expect(panel.props("compact")).toBe(true);
  });

  it("starts on the Reporting page's default period", () => {
    expect(panelOf(mountBlock()).props("period")).toBe(REPORTING_DEFAULT_PERIOD);
  });

  it("remembers a picked period for the next customer", async () => {
    await panelOf(mountBlock("org-1")).vm.$emit("update:period", "q2");

    expect(panelOf(mountBlock("org-2")).props("period")).toBe("q2");
  });

  it("falls back to the default when storage holds no known period", () => {
    localStorage.setItem(CUSTOMER_SALES_PERIOD_STORAGE_KEY, "decade");

    expect(panelOf(mountBlock()).props("period")).toBe(REPORTING_DEFAULT_PERIOD);
  });

  it("opens the Reporting page on this customer and period", async () => {
    const wrapper = mountBlock("org-1");
    await panelOf(wrapper).vm.$emit("update:period", "12m");

    expect(linkOf(wrapper)).toEqual({ name: REPORTING_ROUTE_NAME, query: { customer: "org-1", period: "12m" } });
  });
});
