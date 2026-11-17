import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { COUPONS_DEFAULT_ROWS, CUSTOMER_COUPONS_ROUTE_NAME } from "../constants";
import SalesRepCoupons from "./sales-rep-coupons.vue";
import type { SalesRepCouponType } from "../composables/useSalesRepCoupons";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    coupons: ref<SalesRepCouponType[]>([]),
    loading: ref(false),
    failed: ref(false),
    useSalesRepCoupons: vi.fn(),
  };
});

vi.mock("../composables/useSalesRepCoupons", () => ({ useSalesRepCoupons: state.useSalesRepCoupons }));
vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));

// Plain mount: this file mocks the vue-i18n module, and the shared factory's defaults build a real i18n plugin.
// The list itself is covered by coupon-list.test.ts; here it is a stub that shows what the widget hands it.
function createWrapper(props: { organizationId?: string } = {}) {
  return mount(SalesRepCoupons, {
    props,
    global: {
      stubs: {
        VcWidget: { template: '<div><slot name="append" /><slot /></div>' },
        VcButton: true,
        VcIcon: true,
        VcLink: {
          props: ["to"],
          template: '<a class="all-link" :data-route="to.name" :data-org="to.params.organizationId"><slot /></a>',
        },
        CouponList: {
          props: { compact: Boolean, skeletonRows: Number },
          template: '<div class="list" :data-compact="String(compact)" :data-rows="skeletonRows" />',
        },
      },
    },
  });
}

beforeEach(() => {
  state.useSalesRepCoupons.mockClear();
  state.useSalesRepCoupons.mockImplementation(() => ({
    coupons: state.coupons,
    loading: state.loading,
    failed: state.failed,
  }));
});

describe("SalesRepCoupons", () => {
  it("asks for the default row cap when rendered outside a layout", () => {
    createWrapper();

    const [{ first }] = state.useSalesRepCoupons.mock.calls[0] as [{ first: () => number }];
    expect(first()).toBe(COUPONS_DEFAULT_ROWS);
  });

  it("lists in the compact layout, with a skeleton per row it asked for", () => {
    const list = createWrapper().find(".list");

    expect(list.attributes("data-compact")).toBe("true");
    expect(list.attributes("data-rows")).toBe(String(COUPONS_DEFAULT_ROWS));
  });

  it("links to the customer's all-coupons page", () => {
    const link = createWrapper({ organizationId: "org-1" }).find(".all-link");

    expect(link.attributes("data-route")).toBe(CUSTOMER_COUPONS_ROUTE_NAME);
    expect(link.attributes("data-org")).toBe("org-1");
    expect(link.text()).toBe("sales_rep.coupons.view_all");
  });

  it("has no link outside a customer's profile", () => {
    expect(createWrapper().find(".all-link").exists()).toBe(false);
  });
});
