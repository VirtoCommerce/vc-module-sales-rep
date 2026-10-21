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
    error: ref<Error | null>(null),
    useSalesRepCoupons: vi.fn(),
  };
});

vi.mock("../composables/useSalesRepCoupons", () => ({ useSalesRepCoupons: state.useSalesRepCoupons }));
// Keys, not prose: the assertions are about which branch rendered, and the params it composed.
vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
  }),
}));

function makeCoupon(overrides: Partial<SalesRepCouponType> = {}): SalesRepCouponType {
  return {
    id: "coupon-1",
    couponCode: "SPRING10",
    promotionId: "promo-1",
    name: "Spring tooling promo",
    label: "10% OFF",
    maxUsesNumber: 0,
    maxUsesPerUser: 0,
    totalUsesCount: 0,
    ...overrides,
  };
}

// Plain mount: this file mocks the vue-i18n module, and the shared factory's defaults build a real i18n plugin.
function createWrapper(props: { organizationId?: string } = {}) {
  return mount(SalesRepCoupons, {
    props,
    global: {
      stubs: {
        VcWidget: { template: '<div><slot name="append" /><slot /></div>' },
        VcEmptyView: {
          props: ["text", "variant"],
          template: '<div class="empty" :data-variant="variant">{{ text }}</div>',
        },
        VcButton: true,
        VcIcon: true,
        VcLink: {
          props: ["to"],
          template: '<a class="all-link" :data-route="to.name" :data-org="to.params.organizationId"><slot /></a>',
        },
      },
    },
  });
}

const cards = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll(".coupon-item");

beforeEach(() => {
  state.coupons.value = [];
  state.loading.value = false;
  state.error.value = null;
  state.useSalesRepCoupons.mockClear();
  state.useSalesRepCoupons.mockImplementation(() => ({
    coupons: state.coupons,
    loading: state.loading,
    error: state.error,
  }));
});

describe("SalesRepCoupons", () => {
  it("renders the host's compact card per coupon", () => {
    state.coupons.value = [makeCoupon(), makeCoupon({ id: "coupon-2", couponCode: "FREESHIP" })];

    const wrapper = createWrapper();

    expect(cards(wrapper)).toHaveLength(2);
    expect(cards(wrapper)[1].find(".coupon-item__code-value").text()).toBe("FREESHIP");
    expect(cards(wrapper)[0].attributes("data-compact")).toBe("true");
  });

  it("reads uses left from the cap, and unlimited when there is none", () => {
    state.coupons.value = [
      makeCoupon({ maxUsesNumber: 5, totalUsesCount: 3 }),
      makeCoupon({ id: "coupon-2", maxUsesNumber: 0, totalUsesCount: 40 }),
    ];

    const uses = createWrapper()
      .findAll(".sales-rep-coupons__uses")
      .map((node) => node.text());

    expect(uses).toEqual(['sales_rep.coupons.uses_left {"left":2,"total":5}', "sales_rep.coupons.unlimited"]);
  });

  it("asks for the block's row cap", () => {
    createWrapper();

    const [{ first }] = state.useSalesRepCoupons.mock.calls[0] as [{ first: () => number }];
    expect(first()).toBe(COUPONS_DEFAULT_ROWS);
  });

  it("shows the empty view when the store has no coupons", () => {
    const wrapper = createWrapper();

    expect(wrapper.find(".empty").text()).toBe("sales_rep.coupons.empty");
    expect(cards(wrapper)).toHaveLength(0);
  });

  it("shows skeleton rows on the first load", () => {
    state.loading.value = true;

    const wrapper = createWrapper();

    // The host's own placeholder, in the same compact shape as the cards that replace it.
    expect(wrapper.findAll(".coupon-item-skeleton")).toHaveLength(COUPONS_DEFAULT_ROWS);
    expect(wrapper.find(".coupon-item-skeleton").attributes("data-compact")).toBe("true");
    expect(wrapper.find(".empty").exists()).toBe(false);
  });

  it("names the failure instead of the previous rows", () => {
    state.coupons.value = [makeCoupon()];
    state.error.value = new Error("boom");

    const wrapper = createWrapper();

    expect(wrapper.find(".empty").attributes("data-variant")).toBe("error");
    expect(wrapper.find(".empty").text()).toBe("sales_rep.coupons.load_failed");
    expect(cards(wrapper)).toHaveLength(0);
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

  // VcEmptyView is silent to assistive tech; the widget's own live region, mounted from the start, speaks for it.
  describe("live region", () => {
    const status = (wrapper: ReturnType<typeof createWrapper>) => wrapper.find('[role="status"]');

    it("is mounted from the start, and quiet while loading", () => {
      state.loading.value = true;
      const wrapper = createWrapper();

      expect(status(wrapper).attributes("aria-live")).toBe("polite");
      expect(status(wrapper).text()).toBe("");
    });

    it("announces an empty store", () => {
      expect(status(createWrapper()).text()).toBe("sales_rep.coupons.empty");
    });

    it("announces a failure", () => {
      state.error.value = new Error("boom");

      expect(status(createWrapper()).text()).toBe("sales_rep.coupons.load_failed");
    });

    it("stays quiet over listed coupons", () => {
      state.coupons.value = [makeCoupon()];

      expect(status(createWrapper()).text()).toBe("");
    });
  });
});
