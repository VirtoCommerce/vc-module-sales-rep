import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerBrowseHistory from "./customer-browse-history.vue";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    items: ref<Record<string, unknown>[]>([]),
    unavailable: ref(false),
    loading: ref(false),
    error: ref<Error | null>(null),
  };
});

const browseOptions = vi.hoisted(() => ({ last: undefined as Record<string, unknown> | undefined }));
vi.mock("../composables/useSalesRepBrowseHistory", () => ({
  useSalesRepBrowseHistory: (options: Record<string, unknown>) => {
    browseOptions.last = options;
    return { ...state };
  },
}));
// The window is captured too: the label and the empty text say "this year", so the request has to mean it.
const periodFilter = vi.hoisted(() => ({ initial: undefined as string | undefined }));
vi.mock("../composables/useSalesRepPeriodFilter", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepPeriodFilter: (initial?: string) => {
      periodFilter.initial = initial;
      return { from: ref(undefined), to: ref(undefined) };
    },
  };
});

const createWrapper = createWrapperFactory(mount, CustomerBrowseHistory, {
  props: { organizationId: "org-1" },
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcEmptyView: true,
      VcIcon: true,
      VcImage: true,
      // Props-exposing stub so the link target can be asserted, not just its presence.
      VcLink: { name: "VcLinkStub", props: ["to"], template: "<a><slot /></a>" },
      SalesRepRuleChips: true,
    },
  },
});

const emptyViews = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll("vc-empty-view-stub");
const rows = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="customer-browse-history-row"]');
const skeletons = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="customer-browse-history-skeleton"]');

beforeEach(() => {
  state.items.value = [];
  state.unavailable.value = false;
  state.loading.value = false;
  state.error.value = null;
  periodFilter.initial = undefined;
});

describe("CustomerBrowseHistory states", () => {
  it("shows the no-data view, not an error, when nothing was tracked this year", () => {
    const views = emptyViews(createWrapper());

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBeUndefined();
    expect(views[0].attributes("text")).toBe("sales_rep.customer_insights.browse_history.empty_this_year");
  });

  it("reads this year and names it beside the rows", () => {
    state.items.value = [{ productId: "p1", name: "Drill", sku: "", imageUrl: "", viewCount: 2 }];

    const wrapper = createWrapper();

    expect(periodFilter.initial).toBe("year");
    expect(wrapper.get('[data-test-id="customer-browse-history-period-label"]').text()).toBe(
      "sales_rep.activity.period.year",
    );
  });

  it("names the unavailable state distinctly from the empty one", () => {
    state.unavailable.value = true;

    const views = emptyViews(createWrapper());

    expect(views).toHaveLength(1);
    expect(views[0].attributes("text")).toBe("sales_rep.customer_insights.analytics_unavailable");
  });

  it("replaces the list with the failure view when the query failed but stale rows remain", () => {
    state.items.value = [{ productId: "p1", name: "Drill", sku: "", imageUrl: "", viewCount: 2 }];
    state.error.value = new Error("boom");

    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(rows(wrapper)).toHaveLength(0);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
  });

  it("shows skeleton rows, not the failure view, while a retry is in flight", () => {
    state.items.value = [{ productId: "p1", name: "Drill", sku: "", imageUrl: "", viewCount: 2 }];
    state.error.value = new Error("boom");
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(emptyViews(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper)).toHaveLength(5);
  });

  // The query keeps the previous answer until the new one lands: Recent's rows must not sit under the Top chip.
  it("replaces the shown rows with skeleton rows while the other sort loads", () => {
    state.items.value = [{ productId: "p1", name: "Drill", sku: "", imageUrl: "", viewCount: 2 }];
    state.loading.value = true;

    const wrapper = createWrapper();

    expect(rows(wrapper)).toHaveLength(0);
    expect(skeletons(wrapper)).toHaveLength(5);
  });

  // GA stores the product code, not the id; only a backend-resolved row may deep-link, and it does so
  // by id — an unresolved one has no linkable id, so it degrades to plain text (design doc §5.1).
  it("links a resolved row by product id and degrades an unresolved one to plain text", () => {
    state.items.value = [
      { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "", isResolved: true, viewCount: 4 },
      { productId: "CODE-2", name: "Mystery", sku: "CODE-2", imageUrl: "", isResolved: false, viewCount: 1 },
    ];

    const wrapper = createWrapper();
    const [resolved, unresolved] = rows(wrapper);

    expect(resolved.findComponent({ name: "VcLinkStub" }).props("to")).toEqual({
      name: "Product",
      params: { productId: "p1" },
    });
    expect(unresolved.find("a").exists()).toBe(false);
    expect(unresolved.text()).toContain("Mystery");
  });

  // A resolved product can come back with an empty name: the code stands in, in the link and its title.
  it("names a resolved product by its code when it has no name", () => {
    state.items.value = [{ productId: "p1", name: "", sku: "SKU-1", imageUrl: "", isResolved: true, viewCount: 1 }];

    const link = createWrapper().findComponent({ name: "VcLinkStub" });

    expect(link.text()).toBe("SKU-1");
    expect(link.attributes("title")).toBe("SKU-1");
  });

  it("shows the code under a product's name, and not twice when it stands in for the name", () => {
    state.items.value = [
      { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "", isResolved: true, viewCount: 1 },
      { productId: "p2", name: "", sku: "SKU-2", imageUrl: "", isResolved: true, viewCount: 1 },
    ];

    const [named, unnamed] = rows(createWrapper());

    expect(named.get('[data-test-id="customer-browse-history-sku-label"]').text()).toBe("SKU-1");
    expect(unnamed.find('[data-test-id="customer-browse-history-sku-label"]').exists()).toBe(false);
  });

  // Beside the product's name, a picture of it says nothing more: decorative.
  it("shows the product image without alt text, and a placeholder icon without one", () => {
    state.items.value = [
      {
        productId: "p1",
        name: "Drill",
        sku: "SKU-1",
        imageUrl: "https://cdn/drill.png",
        isResolved: true,
        viewCount: 1,
      },
      { productId: "p2", name: "Saw", sku: "SKU-2", imageUrl: "", isResolved: true, viewCount: 1 },
    ];

    const [withImage, withoutImage] = rows(createWrapper());

    expect(withImage.get("vc-image-stub").attributes("alt")).toBe("");
    expect(withoutImage.find("vc-image-stub").exists()).toBe(false);
    expect(withoutImage.get("vc-icon-stub").attributes("name")).toBe("cube");
  });
});
