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
    error: ref<Error | null>(null),
    customer: ref<{ organizationName: string } | undefined>({ organizationName: "MERCURY123" }),
    notFound: ref(false),
    useSalesRepCoupons: vitest.fn(),
  };
});

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

function makeCoupon(id: string): SalesRepCouponType {
  return {
    id,
    couponCode: id.toUpperCase(),
    promotionId: `p-${id}`,
    maxUsesNumber: 0,
    maxUsesPerUser: 0,
    totalUsesCount: 0,
  };
}

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
      },
    },
  });
}

const cards = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll(".coupon-item");
const pagerProps = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findComponent({ name: "VcPaginationStub" }).props() as { page: number; pages: number };
const requestedPage = () => {
  const [{ first, after }] = state.useSalesRepCoupons.mock.calls.at(-1) as [{ first: number; after: () => string }];
  return { first, after: after() };
};

vi.stubGlobal("scroll", vi.fn());

enableAutoUnmount(afterEach);

beforeEach(() => {
  state.coupons.value = [makeCoupon("c1"), makeCoupon("c2")];
  state.totalCount.value = 2;
  state.loading.value = false;
  state.error.value = null;
  state.customer.value = { organizationName: "MERCURY123" };
  state.notFound.value = false;
  state.useSalesRepCoupons.mockClear();
  state.useSalesRepCoupons.mockImplementation(() => ({
    coupons: state.coupons,
    totalCount: state.totalCount,
    loading: state.loading,
    error: state.error,
  }));
});

describe("CustomerCoupons page", () => {
  it("lists the host's regular (not compact) card per coupon, under the customer's name", () => {
    const wrapper = createWrapper();

    expect(cards(wrapper)).toHaveLength(2);
    expect(cards(wrapper)[0].attributes("data-compact")).toBe("false");
    expect(wrapper.find("h1").text()).toBe('sales_rep.coupons.page.title {"customer":"MERCURY123"}');
  });

  it("reads the first page, and pages by offset", async () => {
    state.totalCount.value = 25;

    const wrapper = createWrapper();

    expect(requestedPage()).toEqual({ first: COUPONS_PAGE_SIZE, after: "0" });
    expect(pagerProps(wrapper).pages).toBe(3);

    wrapper.findComponent({ name: "VcPaginationStub" }).vm.$emit("update:page", 3);
    await wrapper.vm.$nextTick();

    expect(requestedPage().after).toBe(String(2 * COUPONS_PAGE_SIZE));
  });

  // Regression: apollo drops the result while the next page loads, so the total briefly reads 0.
  it("stays on the requested page while it loads", async () => {
    state.totalCount.value = 25;
    const wrapper = createWrapper();

    wrapper.findComponent({ name: "VcPaginationStub" }).vm.$emit("update:page", 2);
    state.loading.value = true;
    state.totalCount.value = 0;
    await wrapper.vm.$nextTick();

    state.loading.value = false;
    state.totalCount.value = 25;
    await wrapper.vm.$nextTick();

    expect(requestedPage().after).toBe(String(COUPONS_PAGE_SIZE));
  });

  it("steps back to the last page when the list shrinks under it", async () => {
    state.totalCount.value = 25;
    const wrapper = createWrapper();

    wrapper.findComponent({ name: "VcPaginationStub" }).vm.$emit("update:page", 3);
    await wrapper.vm.$nextTick();
    state.totalCount.value = 12;
    await wrapper.vm.$nextTick();

    expect(requestedPage().after).toBe(String(COUPONS_PAGE_SIZE));
  });

  it("shows the host's regular skeleton cards on the first load", () => {
    state.coupons.value = [];
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(wrapper.findAll(".coupon-item-skeleton")).toHaveLength(COUPONS_PAGE_SIZE);
    expect(wrapper.find(".coupon-item-skeleton").attributes("data-compact")).toBe("false");
  });

  it("hides the pager on a single page", () => {
    expect(createWrapper().find(".pager").exists()).toBe(false);
  });

  it("names a customer the rep does not serve, with the way back to the profile", () => {
    state.notFound.value = true;

    const wrapper = createWrapper();

    expect(wrapper.find(".empty").text()).toContain("sales_rep.customer_profile.not_found");
    expect(wrapper.find(".back").attributes("data-route")).toBe(CUSTOMER_PROFILE_ROUTE_NAME);
    expect(cards(wrapper)).toHaveLength(0);
  });

  it("names the failure instead of the previous rows", () => {
    state.error.value = new Error("boom");

    const wrapper = createWrapper();

    expect(wrapper.find(".empty").attributes("data-variant")).toBe("error");
    expect(cards(wrapper)).toHaveLength(0);
  });

  it("shows the empty view when the store has no coupons", () => {
    state.coupons.value = [];
    state.totalCount.value = 0;

    expect(createWrapper().find(".empty").text()).toBe("sales_rep.coupons.empty");
  });

  // VcEmptyView is silent to assistive tech; the page's own live region, mounted from the start, speaks for it.
  describe("live region", () => {
    const status = (wrapper: ReturnType<typeof createWrapper>) => wrapper.find('[role="status"]');

    it("stays quiet over listed coupons and while loading", () => {
      expect(status(createWrapper()).text()).toBe("");

      state.coupons.value = [];
      state.loading.value = true;

      expect(status(createWrapper()).text()).toBe("");
    });

    it.each([
      [
        "an empty store",
        () => {
          state.coupons.value = [];
          state.totalCount.value = 0;
        },
        "sales_rep.coupons.empty",
      ],
      [
        "a failure",
        () => {
          state.error.value = new Error("boom");
        },
        "sales_rep.coupons.load_failed",
      ],
      [
        "a customer the rep does not serve",
        () => {
          state.notFound.value = true;
        },
        "sales_rep.customer_profile.not_found",
      ],
    ])("announces %s", (_name, arrange, expected) => {
      arrange();

      expect(status(createWrapper()).text()).toBe(expected);
    });
  });
});
