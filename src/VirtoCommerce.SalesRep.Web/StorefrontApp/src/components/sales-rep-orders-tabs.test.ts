import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { computed, defineComponent, h, nextTick, reactive, ref } from "vue";
import SalesRepOrders from "./sales-rep-orders.vue";
import type { SalesRepRuleType } from "../types";
import type { BlockSettingsType, ILayoutBlockChromeType } from "@vc-frontend/core";

const CATALOG: SalesRepRuleType[] = [
  { name: "New", label: "New" },
  { name: "Processing", label: "Processing" },
];

// The only apollo consumers here; the sort and period composables are pure.
const mocks = vi.hoisted(() => ({
  filterRules: [] as SalesRepRuleType[],
  ordersOptions: undefined as { filter?: () => string | undefined; first?: () => number | undefined } | undefined,
  // What `useBlockChrome()` hands the widget: the layout block's offer, built per test by `mountOrders`.
  chrome: undefined as ILayoutBlockChromeType | undefined,
}));

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key, te: () => false }) }));

// The chrome is the host engine's provide/inject seam; the widget only ever sees what `useBlockChrome` returns.
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useBlockChrome: () => mocks.chrome,
}));

vi.mock("../composables/useSalesRepRules", () => ({
  useSalesRepRules: (_domain: string, kind: string) => ({
    rules: ref(kind === "filter" ? mocks.filterRules : []),
    loading: ref(false),
  }),
}));

vi.mock("../composables/useSalesRepOrders", () => ({
  useSalesRepOrders: (options: typeof mocks.ordersOptions) => {
    mocks.ordersOptions = options;
    return { orders: ref([]), loading: ref(false), error: ref<Error | null>(null) };
  },
}));

/** Mirrors the real label>button structure so the chips' class lands where it does in the app. */
const VcTabSwitchStub = defineComponent({
  name: "VcTabSwitch",
  props: { value: { type: null, default: undefined }, modelValue: { type: null, default: undefined } },
  emits: { change: (value: unknown) => value !== undefined },

  setup(props, { emit, slots }) {
    return () =>
      h("label", null, [
        h("button", { type: "button", onClick: () => emit("change", props.value) }, slots.default?.()),
      ]);
  },
});

const global = {
  stubs: {
    LayoutWidget: { template: '<div><slot name="default-container" /></div>' },
    VcTabSwitch: VcTabSwitchStub,
    VcEmptyView: true,
    VcTable: true,
    VcTableColumn: true,
    VcIcon: true,
    VcLink: true,
    VcTooltip: true,
  },
};

const TABS = ".sales-rep-rule-chip";

function mountOrders(options: { editing?: boolean; draft?: string[]; saved?: string[] } = {}) {
  const editing = ref(options.editing ?? false);
  const draft = reactive<BlockSettingsType>({ maxRows: 5, hiddenTabs: options.draft ?? [] });
  const saved = reactive<BlockSettingsType>({ maxRows: 5, hiddenTabs: options.saved ?? [] });
  const update = vi.fn((patch: Partial<BlockSettingsType>) => Object.assign(draft, patch));

  // The block's offer as the host's LayoutBlock makes it: the draft and the saved settings side by side,
  // `editing` for the mode, and `updateSettings` as the one way to write.
  mocks.chrome = {
    draggable: computed(() => editing.value),
    grabbed: computed(() => false),
    title: computed(() => "Recent orders"),
    hide: vi.fn(),
    handleKeydown: vi.fn(),
    handleBlur: vi.fn(),
    editing: computed(() => editing.value),
    settings: computed(() => draft),
    savedSettings: computed(() => saved),
    maxRows: computed(() => ({ kind: "maxRows" as const, default: 5, min: 1, max: 20 })),
    updateSettings: update,
  };

  const wrapper = mount(SalesRepOrders, { props: { title: "Recent orders", filterable: true }, global });

  return { wrapper, editing, draft, saved, update, filter: () => mocks.ordersOptions?.filter?.() };
}

describe("the order status tabs of a widget inside a layout", () => {
  it("keeps the chosen chip when the rep unchecks that tab and cancels", async () => {
    mocks.filterRules = CATALOG;
    const { wrapper, editing, draft, filter } = mountOrders();

    await wrapper.findAll(TABS)[2].find("button").trigger("click");
    expect(filter()).toBe("Processing");

    // Unchecked while editing: the draft moves, the saved document does not.
    editing.value = true;
    await nextTick();
    draft.hiddenTabs = ["Processing"];
    await nextTick();
    expect(filter()).toBe("Processing");

    // Cancel.
    draft.hiddenTabs = [];
    editing.value = false;
    await nextTick();
    expect(filter()).toBe("Processing");
    expect(wrapper.findAll(TABS)).toHaveLength(3);
  });

  // The chips' count element is opt-in (documents category tabs); rules without counts render none.
  it("renders no count element when the rules carry no counts", () => {
    mocks.filterRules = CATALOG;
    const { wrapper } = mountOrders();

    expect(wrapper.find(".sales-rep-rule-chip__count").exists()).toBe(false);
  });

  it("clears the chosen chip once the tab is saved away", async () => {
    mocks.filterRules = CATALOG;
    const { wrapper, saved, filter } = mountOrders();

    await wrapper.findAll(TABS)[2].find("button").trigger("click");
    expect(filter()).toBe("Processing");

    saved.hiddenTabs = ["Processing"];
    await nextTick();

    expect(filter()).toBeUndefined();
    expect(wrapper.findAll(TABS)).toHaveLength(2);
  });

  // A tray restore: edit mode already on, catalog already cached, so neither watch source changes again.
  it("prunes a retired tab name on a mount that is already in edit mode", async () => {
    mocks.filterRules = CATALOG;
    const { update } = mountOrders({ editing: true, draft: ["Processing", "Retired"] });

    await nextTick();

    expect(update).toHaveBeenCalledWith({ hiddenTabs: ["Processing"] });
  });

  it("leaves the selection alone on a mount outside edit mode", async () => {
    mocks.filterRules = CATALOG;
    const { update } = mountOrders({ draft: ["Processing", "Retired"] });

    await nextTick();

    expect(update).not.toHaveBeenCalled();
  });

  // An empty catalog means the query has not resolved, not that every status was retired.
  it("prunes nothing while the catalog is still unloaded", async () => {
    mocks.filterRules = [];
    const { update } = mountOrders({ editing: true, draft: ["Processing"] });

    await nextTick();

    expect(update).not.toHaveBeenCalled();
  });
});
