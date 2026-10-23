import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import { DASHBOARD_ROUTE_NAME } from "../constants";
import CustomerProfile from "./customer-profile.vue";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    customer: ref<Record<string, unknown> | undefined>(undefined),
    loading: ref(false),
    failed: ref(false),
    notFound: ref(false),
  };
});

vi.mock("../composables/useSalesRepCustomer", () => ({
  useSalesRepCustomer: () => ({
    customer: state.customer,
    loading: state.loading,
    failed: state.failed,
    notFound: state.notFound,
  }),
}));
// The gate the page hands the widgets: their statistics queries run only while it holds.
const widgets = vi.hoisted(() => ({ settled: undefined as { value: boolean } | undefined }));

vi.mock("../composables/useSalesRepCustomerWidgets", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepCustomerWidgets: (visibility: { settled: { value: boolean } }) => {
      widgets.settled = visibility.settled;
      return { cards: ref([]) };
    },
  };
});
vi.mock("@vc-frontend/core", async (importOriginal) => {
  const { computed, unref } = await import("vue");
  return {
    ...(await importOriginal<Record<string, unknown>>()),
    useBreadcrumbs: (sources: unknown) =>
      computed(() => (typeof sources === "function" ? (sources as () => IBreadcrumb[])() : unref(sources))),
    usePageHead: vi.fn(),
  };
});

const createWrapper = createWrapperFactory(mount, CustomerProfile, {
  props: { organizationId: "org-1" },
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcBreadcrumbs: { props: ["items"], template: '<nav class="crumbs" />' },
      VcEmptyView: true,
      VcButton: true,
      VcTypography: true,
      VcImage: true,
      VcIcon: true,
      LayoutSurface: true,
    },
  },
});

const emptyView = (wrapper: ReturnType<typeof createWrapper>) => wrapper.find("vc-empty-view-stub");

beforeEach(() => {
  state.customer.value = undefined;
  state.loading.value = false;
  state.failed.value = false;
  state.notFound.value = false;
  widgets.settled = undefined;
});

describe("CustomerProfile states", () => {
  it("links the hub breadcrumb to the Sales Rep dashboard", () => {
    const wrapper = createWrapper();

    // findComponent by selector is typed as WrapperLike, which exposes no props.
    const stub = wrapper.findComponent("nav.crumbs") as unknown as { props: (key: string) => IBreadcrumb[] };
    const items = stub.props("items");
    const hub = items.find((item) => item.title === "sales_rep.hub.title");

    expect(hub?.route).toEqual({ name: DASHBOARD_ROUTE_NAME });
  });

  it("shows the profile once the customer resolved", () => {
    state.customer.value = { organizationId: "org-1", organizationName: "Acme" };

    const wrapper = createWrapper();

    expect(emptyView(wrapper).exists()).toBe(false);
    expect(wrapper.find("layout-surface-stub").exists()).toBe(true);
  });

  it("words an unserved or unknown organization as not found", () => {
    state.notFound.value = true;

    const wrapper = createWrapper();

    expect(emptyView(wrapper).attributes("text")).toBe("sales_rep.customer_profile.not_found");
    expect(emptyView(wrapper).attributes("variant")).toBe("empty");
  });

  // A server failure used to land in the not-found view, telling the rep this customer isn't theirs.
  it("words a failed read as a load failure, not as not found", () => {
    state.failed.value = true;

    const wrapper = createWrapper();

    expect(emptyView(wrapper).attributes("text")).toBe("sales_rep.customer_profile.load_failed");
    expect(emptyView(wrapper).attributes("variant")).toBe("error");
    expect(wrapper.find("layout-surface-stub").exists()).toBe(false);
  });
});

// No surface renders for a customer that failed to load or is not the rep's, so their cards' figures are never
// asked for either.
describe("CustomerProfile statistics", () => {
  it("are asked for once the layout settled and the customer resolved", () => {
    state.customer.value = { organizationId: "org-1", organizationName: "Acme" };

    createWrapper();

    expect(widgets.settled?.value).toBe(true);
  });

  it("are held back for a customer that failed to load", () => {
    state.failed.value = true;

    createWrapper();

    expect(widgets.settled?.value).toBe(false);
  });

  it("are held back for a customer that is not found", () => {
    state.notFound.value = true;

    createWrapper();

    expect(widgets.settled?.value).toBe(false);
  });
});
