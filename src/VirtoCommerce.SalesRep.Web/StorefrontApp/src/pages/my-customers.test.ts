import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, inject, provide } from "vue";
import type { InjectionKey } from "vue";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import CustomerCommunicationModal from "../components/customer-communication-modal.vue";
import MyCustomers from "./my-customers.vue";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    openModal: vi.fn(),
    items: ref<Record<string, unknown>[]>([]),
    loading: ref(false),
    error: ref<Error | null>(null),
    keyword: ref(""),
    filter: ref<string | undefined>(undefined),
    filterRulesFailed: ref(false),
    sortRulesFailed: ref(false),
  };
});

vi.mock("../composables/useSalesRepCustomers", async () => {
  const { ref } = await import("vue");
  return {
    PAGE_SIZE: 10,
    useSalesRepCustomers: () => ({
      items: state.items,
      loading: state.loading,
      error: state.error,
      keyword: state.keyword,
      filter: state.filter,
      sortRule: ref(undefined),
      page: ref(1),
      pages: ref(1),
    }),
  };
});
vi.mock("../composables/useSalesRepRules", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepRules: (_domain: string, kind: string) => ({
      rules: ref([]),
      loading: ref(false),
      failed: kind === "filter" ? state.filterRulesFailed : state.sortRulesFailed,
    }),
  };
});
vi.mock("../composables/useSalesRepColumnSort", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepColumnSort: () => ({ sortInfo: ref(undefined), isColumnSortable: () => false, applySort: vi.fn() }),
  };
});
vi.mock("../composables/useSalesRepCommunication", () => ({
  useSalesRepCommunication: () => ({ send: vi.fn(), sending: false }),
}));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useModal: () => ({ openModal: state.openModal }),
}));

const createWrapper = createWrapperFactory(mount, MyCustomers, {
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcWidget: { template: '<div><slot name="default-container" /></div>' },
      VcTable: true,
      VcTableColumn: true,
      VcEmptyView: true,
      VcTypography: true,
      VcInput: true,
      VcButton: true,
      VcIcon: true,
      VcLink: true,
      VcTooltip: true,
      // Rendered rather than stubbed away: the assertion is about which message the alert carries.
      VcAlert: { template: '<div class="vc-alert"><slot /></div>' },
      SalesRepRuleChips: true,
      CustomerCommunicationModal: true,
      CustomerCallAction: true,
    },
  },
});

const emptyViews = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findAll("vc-empty-view-stub");

beforeEach(() => {
  state.openModal.mockClear();
  state.items.value = [];
  state.loading.value = false;
  state.error.value = null;
  state.keyword.value = "";
  state.filter.value = undefined;
  state.filterRulesFailed.value = false;
  state.sortRulesFailed.value = false;
});

describe("MyCustomers Send message", () => {
  // A row's envelope opens the popup with that customer; the page's own button is for picking them in the popup.
  it("opens the message popup with nobody picked", async () => {
    const wrapper = createWrapper();

    await wrapper.find("[data-test-id='my-customers-send-message-button']").trigger("click");

    expect(state.openModal).toHaveBeenCalledWith({ component: CustomerCommunicationModal, props: {} });
  });
});

// The segment chips and the sortable headers vanish without their rules, with nothing saying why (VCST-5682).
describe("MyCustomers degraded controls", () => {
  it("says the segment filters could not be loaded", () => {
    state.filterRulesFailed.value = true;

    const wrapper = createWrapper();

    expect(wrapper.find(".vc-alert").text()).toContain("sales_rep.rules.load_failed.filter");
  });

  it("keeps quiet while the rules load", () => {
    const wrapper = createWrapper();

    expect(wrapper.find(".vc-alert").exists()).toBe(false);
  });
});

describe("MyCustomers states", () => {
  it("shows the no-data view, not an error, when the response was simply empty", () => {
    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("empty");
  });

  // The query runs with keepPreviousResult, so rows survive a failed refetch and would otherwise be
  // presented as the current result (VCST-5586).
  it("replaces the table with the failure view when the query failed but stale rows remain", () => {
    state.items.value = [{ organizationId: "o1", organizationName: "Acme", ytdTotal: "$1.00", ytdCount: 1 }];
    state.error.value = new Error("boom");

    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(wrapper.find("vc-table-stub").exists()).toBe(false);
    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
  });

  // With a keyword active the no-data view renders its search variant plus a "Reset search" button —
  // offering the rep a fix for a problem the search did not cause.
  it("shows the failure view rather than the search empty state when a keyword is active", () => {
    state.keyword.value = "acme";
    state.error.value = new Error("boom");

    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("error");
  });

  it("shows the search empty state when a keyword matched nothing and no request failed", () => {
    state.keyword.value = "acme";

    const wrapper = createWrapper();
    const views = emptyViews(wrapper);

    expect(views).toHaveLength(1);
    expect(views[0].attributes("variant")).toBe("search");
  });
});

type RowType = Record<string, unknown>;

const ROW: InjectionKey<RowType> = Symbol("row");

/** Renders each row the way VcTable does: the mobile card from its slot, and every column with the row's item. */
const RowStub = defineComponent({
  props: { item: { type: Object, required: true } },

  setup(props, { slots }) {
    provide(ROW, props.item as RowType);
    return () => slots.default?.();
  },
});

const VcTableStub = defineComponent({
  props: { items: { type: Array, default: () => [] } },

  setup(props, { slots }) {
    return () =>
      h(
        "div",
        (props.items as RowType[]).map((item) =>
          h(RowStub, { item, key: String(item.organizationId) }, () => [
            h("div", { class: "mobile" }, slots["mobile-item"]?.({ item })),
            h("div", { class: "desktop" }, slots.default?.()),
          ]),
        ),
      );
  },
});

const VcTableColumnStub = defineComponent({
  props: { id: { type: String, required: true } },

  setup(props, { slots }) {
    const item = inject(ROW);
    return () => h("div", { class: `column-${props.id}` }, slots.default?.({ item }));
  },
});

const rowStubs = {
  VcTable: VcTableStub,
  VcTableColumn: VcTableColumnStub,
  VcButton: { template: "<button class='button'><slot /></button>" },
  VcTooltip: {
    template:
      "<div class='tooltip'><slot :trigger-props='{}' /><span class='tooltip-text'><slot name='content' /></span></div>",
  },
  CustomerCallAction: {
    props: ["phones", "variant"],
    template: "<span class='call' :data-variant='variant ?? \"icon\"' :data-count='phones.length' />",
  },
};

describe("MyCustomers row actions", () => {
  const phone = { name: "Jane Smith", number: "+1 555 010 2030", href: "tel:+15550102030" };

  beforeEach(() => {
    state.items.value = [
      { organizationId: "o1", organizationName: "Acme", ytdTotal: "$1.00", ytdCount: 1, phones: [phone, phone] },
    ];
  });

  it("puts the call action beside the message action in the table, both under design-system tooltips", () => {
    const wrapper = createWrapper({ global: { stubs: rowStubs } });
    const actions = wrapper.find(".desktop .column-actions");

    expect(actions.find(".button").attributes("aria-label")).toBe("sales_rep.communication.action");
    expect(actions.find(".tooltip-text").text()).toBe("sales_rep.communication.action");
    expect(actions.find(".button").attributes("title")).toBeUndefined();
    expect(actions.find(".call").attributes()).toMatchObject({ "data-variant": "icon", "data-count": "2" });
  });

  it("gives the mobile card a labelled Send message button and the labelled call button", () => {
    const wrapper = createWrapper({ global: { stubs: rowStubs } });
    const card = wrapper.find(".mobile");

    expect(card.find(".button").text()).toBe("sales_rep.communication.action");
    expect(card.find(".call").attributes()).toMatchObject({ "data-variant": "button", "data-count": "2" });
  });
});
