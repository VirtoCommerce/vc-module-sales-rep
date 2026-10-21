import { enableAutoUnmount, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { COUPONS_PAGE_SIZE, CUSTOMER_PROFILE_ROUTE_NAME } from "../constants";
import CustomerCoupons from "./customer-coupons.vue";
import type { SalesRepCouponType } from "../composables/useSalesRepCoupons";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  const { vi: vitest } = await import("vitest");
  return {
    coupons: ref<SalesRepCouponType[]>([]),
    totalCount: ref(0),
    loading: ref(false),
    failed: ref(false),
    customer: ref<{ organizationName: string } | undefined>({ organizationName: "MERCURY123" }),
    notFound: ref(false),
    useSalesRepCoupons: vitest.fn(),
  };
});

// The page's composable runs for real over these two, so its paging is under test here too.
vi.mock("../composables/useSalesRepCoupons", () => ({ useSalesRepCoupons: state.useSalesRepCoupons }));
vi.mock("../composables/useSalesRepCustomer", () => ({
  useSalesRepCustomer: () => ({ customer: state.customer, notFound: state.notFound }),
}));
// Keys, not prose: the assertions are about which branch rendered, and the params it composed.
vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
  }),
}));

// The list itself is covered by coupon-list.test.ts; here it is a stub that shows what the page hands it.
function createWrapper() {
  return mount(CustomerCoupons, {
    props: { organizationId: "org-1" },
    global: {
      stubs: {
        VcBreadcrumbs: { props: ["items"], template: '<nav class="crumbs" />' },
        VcTypography: { template: "<h1><slot /></h1>" },
        VcEmptyView: {
          props: ["text", "variant"],
          template: '<div class="empty" :data-variant="variant">{{ text }}<slot name="button" /></div>',
        },
        VcButton: { props: ["to"], template: '<a class="back" :data-route="to.name"><slot /></a>' },
        VcPagination: { name: "VcPaginationStub", props: ["page", "pages"], template: "<nav class='pager' />" },
        CouponList: {
          props: { compact: Boolean, skeletonRows: Number },
          template: '<div class="list" :data-compact="String(compact)" :data-rows="skeletonRows" />',
        },
      },
    },
  });
}

const pager = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findComponent({ name: "VcPaginationStub" });
const requestedAfter = () => {
  const [{ after }] = state.useSalesRepCoupons.mock.calls.at(-1) as [{ after: () => string }];
  return after();
};

vi.stubGlobal("scroll", vi.fn());

enableAutoUnmount(afterEach);

beforeEach(() => {
  state.coupons.value = [];
  state.totalCount.value = 2;
  state.loading.value = false;
  state.failed.value = false;
  state.customer.value = { organizationName: "MERCURY123" };
  state.notFound.value = false;
  state.useSalesRepCoupons.mockClear();
  state.useSalesRepCoupons.mockImplementation(() => ({
    coupons: state.coupons,
    totalCount: state.totalCount,
    loading: state.loading,
    failed: state.failed,
  }));
});

describe("CustomerCoupons page", () => {
  it("lists in the regular layout, under the customer's name", () => {
    const wrapper = createWrapper();

    expect(wrapper.find(".list").attributes("data-compact")).toBe("false");
    expect(wrapper.find(".list").attributes("data-rows")).toBe(String(COUPONS_PAGE_SIZE));
    expect(wrapper.find("h1").text()).toBe('sales_rep.coupons.page.title {"customer":"MERCURY123"}');
  });

  it("reads the first page, and pages by offset", async () => {
    state.totalCount.value = 25;

    const wrapper = createWrapper();

    expect(requestedAfter()).toBe("0");
    expect(pager(wrapper).props("pages")).toBe(3);

    pager(wrapper).vm.$emit("update:page", 3);
    await wrapper.vm.$nextTick();

    expect(requestedAfter()).toBe(String(2 * COUPONS_PAGE_SIZE));
  });

  it("steps back to the last page when the list shrinks under it", async () => {
    state.totalCount.value = 25;
    const wrapper = createWrapper();

    pager(wrapper).vm.$emit("update:page", 3);
    await wrapper.vm.$nextTick();
    state.totalCount.value = 12;
    await wrapper.vm.$nextTick();

    expect(requestedAfter()).toBe(String(COUPONS_PAGE_SIZE));
  });

  it("hides the pager on a single page", () => {
    expect(createWrapper().find(".pager").exists()).toBe(false);
  });

  it("names a customer the rep does not serve, with the way back to the profile, and announces it", () => {
    state.notFound.value = true;

    const wrapper = createWrapper();

    expect(wrapper.find(".empty").text()).toContain("sales_rep.customer_profile.not_found");
    expect(wrapper.find(".back").attributes("data-route")).toBe(CUSTOMER_PROFILE_ROUTE_NAME);
    expect(wrapper.find('[role="status"]').text()).toBe("sales_rep.customer_profile.not_found");
    expect(wrapper.find(".list").exists()).toBe(false);
  });

  it("stays quiet in its own live region over a served customer", () => {
    expect(createWrapper().find('[role="status"]').text()).toBe("");
  });
});
