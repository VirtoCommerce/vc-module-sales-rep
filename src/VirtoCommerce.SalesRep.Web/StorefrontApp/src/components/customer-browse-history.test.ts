import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { toValue } from "vue";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerBrowseHistory from "./customer-browse-history.vue";
import CustomerInsightsPanel from "./customer-insights-panel.vue";

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

// The panel's states, skeletons and caveat are the shell's: customer-insights-panel.test.ts.
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

const panel = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findComponent(CustomerInsightsPanel);
const rows = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.findAll('[data-test-id="customer-browse-history-row"]');

beforeEach(() => {
  state.items.value = [];
  state.unavailable.value = false;
  state.loading.value = false;
  state.error.value = null;
  periodFilter.initial = undefined;
});

describe("CustomerBrowseHistory", () => {
  // Its rows carry a thumbnail, so their skeletons stand taller.
  it("gives the panel its own wording, icon, state and row height", () => {
    state.unavailable.value = true;

    expect(panel(createWrapper()).props()).toMatchObject({
      icon: "eye",
      failedText: "sales_rep.customer_insights.browse_history.load_failed",
      emptyText: "sales_rep.customer_insights.browse_history.empty_this_year",
      hasRows: false,
      failed: false,
      unavailable: true,
      loading: false,
      tallRows: true,
    });
  });

  it("reads this year, which the panel names", () => {
    createWrapper();

    expect(periodFilter.initial).toBe("year");
  });

  // The ticket asks for recent product views; Top is the deliberate second look.
  it("asks for the newest views by default, and the most viewed from Top", async () => {
    const wrapper = createWrapper();

    expect(toValue(browseOptions.last?.sort)).toBe("date");

    await panel(wrapper).vm.$emit("update:sort", "count");

    expect(toValue(browseOptions.last?.sort)).toBe("count");
  });

  // GA stores the product code, not the id; only a backend-resolved row may deep-link, and it does so
  // by id — an unresolved one has no linkable id, so it degrades to plain text (design doc §5.1).
  it("links a resolved row by product id and degrades an unresolved one to plain text", () => {
    state.items.value = [
      { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "", viewCount: 4 },
      { productId: "", name: "Mystery", sku: "CODE-2", imageUrl: "", viewCount: 1 },
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

  // In a catalog whose product ids equal their codes, an old `productId !== sku` test read every row as
  // unresolved, so none of them ever got a link.
  it("links a product whose id equals its code", () => {
    state.items.value = [{ productId: "SKU-1", name: "Drill", sku: "SKU-1", imageUrl: "", viewCount: 2 }];

    expect(rows(createWrapper())[0].findComponent({ name: "VcLinkStub" }).exists()).toBe(true);
  });

  // A resolved product can come back with an empty name: the code stands in, in the link and its title.
  it("names a resolved product by its code when it has no name", () => {
    state.items.value = [{ productId: "p1", name: "", sku: "SKU-1", imageUrl: "", viewCount: 1 }];

    const link = createWrapper().findComponent({ name: "VcLinkStub" });

    expect(link.text()).toBe("SKU-1");
    expect(link.attributes("title")).toBe("SKU-1");
  });

  it("shows the code under a product's name, and not twice when it stands in for the name", () => {
    state.items.value = [
      { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "", viewCount: 1 },
      { productId: "p2", name: "", sku: "SKU-2", imageUrl: "", viewCount: 1 },
    ];

    const [named, unnamed] = rows(createWrapper());

    expect(named.get('[data-test-id="customer-browse-history-sku-label"]').text()).toBe("SKU-1");
    expect(unnamed.find('[data-test-id="customer-browse-history-sku-label"]').exists()).toBe(false);
  });

  // Beside the product's name, a picture of it says nothing more: decorative.
  it("shows the product image without alt text, and a placeholder icon without one", () => {
    state.items.value = [
      { productId: "p1", name: "Drill", sku: "SKU-1", imageUrl: "https://cdn/drill.png", viewCount: 1 },
      { productId: "p2", name: "Saw", sku: "SKU-2", imageUrl: "", viewCount: 1 },
    ];

    const [withImage, withoutImage] = rows(createWrapper());

    expect(withImage.get("vc-image-stub").attributes("alt")).toBe("");
    expect(withoutImage.find("vc-image-stub").exists()).toBe(false);
    expect(withoutImage.get("vc-icon-stub").attributes("name")).toBe("cube");
  });
});
