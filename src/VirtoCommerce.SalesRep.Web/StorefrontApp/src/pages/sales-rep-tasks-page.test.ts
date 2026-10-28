import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";
import SalesRepTaskModal from "../components/sales-rep-task-modal.vue";
import { TASKS_FILTERS_DEBOUNCE_MS } from "../constants";
import { emptyTaskFilters, localDayKey, localDayWindow } from "../tasks";
import TasksPage from "./sales-rep-tasks-page.vue";
import type { SalesRepFilterChipType } from "../types";
import type { SalesRepTaskFiltersType, SalesRepTaskType } from "../types/tasks";
import type { Ref } from "vue";
import { VcButton } from "@vc-frontend/core";

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    items: ref<SalesRepTaskType[]>([]),
    loading: ref(false),
    error: ref<Error | null>(null),
    page: ref(1),
    pages: ref(1),
    totalCount: ref(0),
    rules: ref<{ name: string; label: string }[]>([]),
    rulesLoading: ref(false),
    rulesFailed: ref(false),
    customers: [] as { organizationId: string; organizationName: string }[],
    customersFailed: false,
    filterParam: ref(""),
    month: ref("2026-10-01"),
    setMonth: vi.fn(),
    useSalesRepTasks: vi.fn(),
    useSalesRepTaskCalendar: vi.fn(),
    refetch: vi.fn(),
    refetchMarkers: vi.fn(),
    setCompleted: vi.fn(),
    openModal: vi.fn(),
  };
});

// useBreadcrumbs reads the current path to decide whether to prepend a catalog root; the page needs
// nothing else from the router.
vi.mock("vue-router", async () => {
  const actual = await vi.importActual<typeof import("vue-router")>("vue-router");
  return { ...actual, useRoute: () => ({ path: "/company/tasks" }) };
});
// The ?filter= deep link; a plain ref stands in for the route-backed writable computed.
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useRouteQueryParam: () => state.filterParam,
  useModal: () => ({ openModal: state.openModal }),
}));
vi.mock("../composables/useSalesRepTasks", () => ({ useSalesRepTasks: state.useSalesRepTasks }));
vi.mock("../composables/useSalesRepTaskCalendar", () => ({
  useSalesRepTaskCalendar: state.useSalesRepTaskCalendar,
  useMonthAnchor: () => ({ month: state.month, setMonth: state.setMonth }),
}));
vi.mock("../composables/useSalesRepTaskMutations", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepTaskMutations: () => ({
      setCompleted: state.setCompleted,
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      loading: ref(false),
    }),
  };
});
vi.mock("../composables/useSalesRepRules", () => ({
  useSalesRepRules: () => ({ rules: state.rules, loading: state.rulesLoading, failed: state.rulesFailed }),
}));
vi.mock("../composables/useSalesRepCustomerOptions", async () => {
  const { ref } = await import("vue");
  return {
    useSalesRepCustomerOptions: () => ({
      options: ref(state.customers),
      failed: ref(state.customersFailed),
      findOption: (id: string) => state.customers.find((customer) => customer.organizationId === id),
    }),
  };
});
// Typed arguments, so the format a call asked for can be asserted.
const dMock = vi.hoisted(() =>
  vi.fn((value: unknown, format?: string) => (format === "short" ? "Oct 20, 2026" : String(value))),
);
vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
    d: dMock,
    n: String,
    locale: { value: "en-US" },
  }),
}));

const TODAY = localDayKey(new Date());

function makeTask(overrides: Partial<SalesRepTaskType> = {}): SalesRepTaskType {
  return {
    id: "task-1",
    name: "Call ACME about the renewal",
    description: "",
    type: "Finance",
    priority: "Normal",
    dueDate: "2026-10-15T00:00:00Z",
    isActive: true,
    completed: undefined,
    status: "upcoming",
    ...overrides,
  };
}

// Named stubs, so the tests can read what the page handed each child and emit back through it.
const BreadcrumbsStub = {
  name: "VcBreadcrumbs",
  props: ["items"],
  template: '<nav class="crumbs" />',
};

const SearchStub = {
  name: "VcInput",
  props: ["modelValue", "disabled", "placeholder"],
  emits: ["update:modelValue", "clear"],
  template:
    '<div class="search"><input class="search-input" :value="modelValue"' +
    ' @input="$emit(\'update:modelValue\', $event.target.value)" /><slot name="append" /></div>',
};

const FiltersStub = {
  name: "SalesRepTaskFilters",
  props: ["modelValue", "statuses", "customers", "customersFailed"],
  emits: ["update:modelValue"],
  template: '<div class="filters" />',
};

const ChipsStub = {
  name: "SalesRepFilterChips",
  props: ["chips"],
  emits: ["remove", "reset"],
  template: '<div class="chips" />',
};

const ListStub = {
  name: "SalesRepTaskList",
  props: ["tasks", "loading", "busy", "page", "pages"],
  emits: ["toggle", "edit", "update:page"],
  template: '<div class="list" />',
};

const CalendarStub = {
  name: "SalesRepTaskCalendar",
  props: { modelValue: String, month: String, dayMarkers: Object, size: String, clearable: Boolean },
  emits: ["update:modelValue", "update:month"],
  template: '<div class="grid" />',
};

const EmptyViewStub = {
  name: "VcEmptyView",
  props: ["text", "variant", "icon"],
  template: '<div class="empty" :data-text="text" :data-variant="variant"><slot name="button" /></div>',
};

// Plain mount (not createWrapperFactory): this file mocks the vue-i18n module, and the shared factory's
// defaults build a real i18n plugin from it.
function createWrapper() {
  return mount(TasksPage, {
    global: {
      renderStubDefaultSlot: false,
      stubs: {
        // The page fills three different VcWidget slots, including the plain default one in the aside.
        VcWidget: {
          template: '<div><slot name="header-container" /><slot name="default-container" /><slot /></div>',
        },
        VcTypography: { template: "<div><slot /></div>" },
        VcBreadcrumbs: BreadcrumbsStub,
        VcInput: SearchStub,
        SalesRepRuleAlert: true,
        SalesRepTaskFilters: FiltersStub,
        SalesRepFilterChips: ChipsStub,
        SalesRepTaskList: ListStub,
        SalesRepTaskCalendar: CalendarStub,
        VcEmptyView: EmptyViewStub,
        VcIcon: true,
      },
      // Real buttons: the page's actions are genuine <button>s the tests click.
      components: { VcButton },
    },
  });
}

type WrapperType = ReturnType<typeof createWrapper>;

function button(wrapper: WrapperType, key: string) {
  const found = wrapper.findAll("button").find((candidate) => candidate.text().includes(key));
  if (!found) {
    throw new Error(`No button for ${key}`);
  }
  return found;
}

/** The options the page handed useSalesRepTasks: the day window, the filters and the sort. */
function taskOptions() {
  return state.useSalesRepTasks.mock.calls.at(-1)?.[0] as {
    period: Ref<{ from: string; to: string } | undefined>;
    filters: Ref<SalesRepTaskFiltersType>;
    sort: string;
  };
}

/** The filters the page hands the calendar's dots query. */
function markerFilters() {
  return state.useSalesRepTaskCalendar.mock.calls.at(-1)?.[1] as Ref<SalesRepTaskFiltersType>;
}

function chips(wrapper: WrapperType) {
  return wrapper.getComponent(ChipsStub).props("chips") as SalesRepFilterChipType[];
}

/** Lets a filter change reach the list and the dots, which ask with the filters a debounce late. */
async function settle() {
  await flushPromises();
  await vi.advanceTimersByTimeAsync(TASKS_FILTERS_DEBOUNCE_MS);
  await flushPromises();
}

async function setFilters(wrapper: WrapperType, filters: Partial<SalesRepTaskFiltersType>) {
  wrapper.getComponent(FiltersStub).vm.$emit("update:modelValue", { ...emptyTaskFilters(), ...filters });
  await settle();
}

async function pickDay(wrapper: WrapperType, day: string | undefined) {
  wrapper.getComponent(CalendarStub).vm.$emit("update:modelValue", day);
  await flushPromises();
}

function lastModal() {
  return state.openModal.mock.calls.at(-1)?.[0] as {
    component: unknown;
    props: { task?: SalesRepTaskType; defaultDay: string; onSaved: (day?: string) => Promise<void> };
  };
}

beforeEach(() => {
  // Only the timers the debounce runs on: flushPromises still needs a real setImmediate.
  vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
  state.items.value = [];
  state.loading.value = false;
  state.error.value = null;
  state.page.value = 1;
  state.pages.value = 1;
  state.totalCount.value = 0;
  state.rules.value = [];
  state.rulesLoading.value = false;
  state.rulesFailed.value = false;
  state.customers = [];
  state.customersFailed = false;
  state.filterParam.value = "";
  state.month.value = "2026-10-01";
  state.setMonth.mockClear();
  dMock.mockClear();
  state.refetch.mockClear();
  state.refetchMarkers.mockClear();
  state.openModal.mockClear();
  state.setCompleted.mockClear().mockResolvedValue(true);
  state.useSalesRepTasks.mockClear();
  state.useSalesRepTasks.mockImplementation(() => ({
    items: state.items,
    loading: state.loading,
    error: state.error,
    page: state.page,
    pages: state.pages,
    totalCount: state.totalCount,
    refetch: state.refetch,
  }));
  state.useSalesRepTaskCalendar.mockClear();
  state.useSalesRepTaskCalendar.mockImplementation(() => ({
    dayMarkers: ref({}),
    loading: ref(false),
    error: ref(null),
    refetch: state.refetchMarkers,
  }));
});

afterEach(() => {
  vi.useRealTimers();
});

describe("Tasks page filters", () => {
  it("opens on today, with every status, customer and priority", () => {
    const wrapper = createWrapper();

    expect(taskOptions().period.value).toEqual(localDayWindow(TODAY));
    expect(taskOptions().filters.value).toEqual(emptyTaskFilters());
    expect(wrapper.getComponent(FiltersStub).props("modelValue")).toEqual(emptyTaskFilters());
  });

  it("offers the server's status rules in the status select", () => {
    state.rules.value = [
      { name: "upcoming", label: "Upcoming" },
      { name: "overdue", label: "Overdue" },
    ];

    const wrapper = createWrapper();

    expect(wrapper.getComponent(FiltersStub).props("statuses")).toEqual(state.rules.value);
  });

  // One customer list serves the select and the chips' names.
  it("hands the customer select the customers it names the chips by", () => {
    state.customers = [{ organizationId: "org-1", organizationName: "Cabin Co." }];
    state.customersFailed = true;

    const filters = createWrapper().getComponent(FiltersStub);

    expect(filters.props("customers")).toEqual(state.customers);
    expect(filters.props("customersFailed")).toBe(true);
  });

  it("narrows the list by what the selects set", async () => {
    const wrapper = createWrapper();

    await setFilters(wrapper, { status: "overdue", customerIds: ["org-1"], priorities: ["High", "Highest"] });

    expect(taskOptions().filters.value).toEqual({
      keyword: "",
      status: "overdue",
      customerIds: ["org-1"],
      priorities: ["High", "Highest"],
    });
  });

  // The dots mark where the filtered tasks are, on any day: the day is what the rep picks by looking at them.
  it("narrows the calendar dots by the same filters, but not by the day", async () => {
    const wrapper = createWrapper();

    await setFilters(wrapper, { customerIds: ["org-1"] });

    expect(markerFilters()).toBe(taskOptions().filters);
    expect(state.useSalesRepTaskCalendar.mock.calls.at(-1)?.[0]).toBe(state.month);
  });

  // A multi-select stays open for the next pick: the list and the dots ask once the picks settle, the chips at once.
  it("asks the list and the dots once a burst of picks settles", async () => {
    const wrapper = createWrapper();

    for (const customerIds of [["org-1"], ["org-1", "org-2"]]) {
      wrapper.getComponent(FiltersStub).vm.$emit("update:modelValue", { ...emptyTaskFilters(), customerIds });
      await flushPromises();
      await vi.advanceTimersByTimeAsync(TASKS_FILTERS_DEBOUNCE_MS - 1);
    }

    expect(chips(wrapper).map((chip) => chip.id)).toEqual(["day", "customerIds:org-1", "customerIds:org-2"]);
    expect(taskOptions().filters.value.customerIds).toEqual([]);

    await vi.advanceTimersByTimeAsync(1);

    expect(taskOptions().filters.value.customerIds).toEqual(["org-1", "org-2"]);
    expect(markerFilters().value.customerIds).toEqual(["org-1", "org-2"]);
  });

  it("applies the search term from the search button, trimmed", async () => {
    const wrapper = createWrapper();

    await wrapper.get(".search-input").setValue("  renewal  ");
    await wrapper.get('[aria-label="sales_rep.tasks.search_aria"]').trigger("click");
    await settle();

    expect(taskOptions().filters.value.keyword).toBe("renewal");
  });

  it("applies the search term on Enter", async () => {
    const wrapper = createWrapper();

    await wrapper.get(".search-input").setValue("renewal");
    await wrapper.get(".search").trigger("keydown", { key: "Enter" });
    await settle();

    expect(taskOptions().filters.value.keyword).toBe("renewal");
  });

  // Typing alone asks nothing: like every other hub list, the term applies on Enter or the button.
  it("leaves the list alone while the term is only typed", async () => {
    const wrapper = createWrapper();

    await wrapper.get(".search-input").setValue("renewal");
    await settle();

    expect(taskOptions().filters.value.keyword).toBe("");
  });

  it("clears the search term from the field's own clear", async () => {
    const wrapper = createWrapper();
    await wrapper.get(".search-input").setValue("renewal");
    await wrapper.get(".search").trigger("keydown", { key: "Enter" });
    await settle();

    wrapper.getComponent(SearchStub).vm.$emit("clear");
    await settle();

    expect(taskOptions().filters.value.keyword).toBe("");
    expect((wrapper.get(".search-input").element as HTMLInputElement).value).toBe("");
  });
});

describe("Tasks page deep link", () => {
  // The dashboard's overdue notice links to ?filter=overdue. Overdue work is never due today: it opens on every date.
  it("opens on the linked status and every date", () => {
    state.filterParam.value = "overdue";
    state.rules.value = [{ name: "overdue", label: "Overdue" }];

    createWrapper();

    expect(taskOptions().filters.value.status).toBe("overdue");
    expect(taskOptions().period.value).toBeUndefined();
  });

  // The filters live in the page, so a parameter left behind would describe a list the rep has since changed.
  it("drops the parameter from the URL once read", () => {
    state.filterParam.value = "overdue";
    state.rules.value = [{ name: "overdue", label: "Overdue" }];

    createWrapper();

    expect(state.filterParam.value).toBe("");
  });

  it("falls back to every status when the project does not offer the linked one", () => {
    state.filterParam.value = "overdue";
    state.rules.value = [{ name: "upcoming", label: "Upcoming" }];

    createWrapper();

    expect(taskOptions().filters.value.status).toBeUndefined();
  });

  // An answer still on its way is not an answer that lacks the rule.
  it("keeps the linked status while the rules are still loading", async () => {
    state.filterParam.value = "overdue";
    state.rulesLoading.value = true;

    createWrapper();
    await flushPromises();

    expect(taskOptions().filters.value.status).toBe("overdue");
  });
});

describe("Tasks page day", () => {
  it("narrows the list to the day picked on the calendar", async () => {
    const wrapper = createWrapper();

    await pickDay(wrapper, "2026-10-20");

    expect(taskOptions().period.value).toEqual(localDayWindow("2026-10-20"));
    expect(wrapper.getComponent(CalendarStub).props("modelValue")).toBe("2026-10-20");
  });

  // The calendar's Clear, or a second click on the picked day, keeps every other filter.
  it("lists every date once the calendar clears the day", async () => {
    const wrapper = createWrapper();
    await setFilters(wrapper, { priorities: ["High"] });

    await pickDay(wrapper, undefined);

    expect(taskOptions().period.value).toBeUndefined();
    expect(taskOptions().filters.value.priorities).toEqual(["High"]);
  });

  it("lets the calendar clear the day", () => {
    const wrapper = createWrapper();

    expect(wrapper.getComponent(CalendarStub).props("clearable")).toBe(true);
  });
});

describe("Tasks page chips", () => {
  it("names today's chip Due today", () => {
    const wrapper = createWrapper();

    expect(chips(wrapper)).toEqual([
      expect.objectContaining({ id: "day", label: "sales_rep.tasks.filters.due_today" }),
    ]);
  });

  // "short" (Oct 20, 2026), not "long": the long named format appends a time, and the chip names a DAY.
  it("names another day's chip by its date", async () => {
    const wrapper = createWrapper();

    await pickDay(wrapper, "2026-10-20");

    expect(chips(wrapper)[0].label).toBe('sales_rep.tasks.due_relative.due {"date":"Oct 20, 2026"}');
    expect(dMock.mock.calls.every(([, format]) => format === "short")).toBe(true);
  });

  it("follows the day with a chip per customer, then the status and the priorities", async () => {
    state.customers = [
      { organizationId: "org-1", organizationName: "Cabin Co." },
      { organizationId: "org-2", organizationName: "Greenleaf Garden Center" },
    ];
    state.rules.value = [{ name: "overdue", label: "Overdue" }];
    const wrapper = createWrapper();

    await setFilters(wrapper, { status: "overdue", customerIds: ["org-1", "org-2"], priorities: ["High"] });

    expect(chips(wrapper).map((chip) => chip.label)).toEqual([
      "sales_rep.tasks.filters.due_today",
      "Cabin Co.",
      "Greenleaf Garden Center",
      "Overdue",
      "sales_rep.tasks.priority.high",
    ]);
  });

  it("names a customer the options do not list by its id", async () => {
    const wrapper = createWrapper();

    await setFilters(wrapper, { customerIds: ["org-9"] });

    expect(chips(wrapper)[1].label).toBe("org-9");
  });

  it("removes only the filter of the chip closed", async () => {
    const wrapper = createWrapper();
    await setFilters(wrapper, { status: "overdue", customerIds: ["org-1", "org-2"], priorities: ["Low", "High"] });

    for (const id of ["customerIds:org-1", "priorities:High", "status:overdue"]) {
      wrapper.getComponent(ChipsStub).vm.$emit(
        "remove",
        chips(wrapper).find((chip) => chip.id === id),
      );
      await settle();
    }

    expect(taskOptions().filters.value).toEqual({
      keyword: "",
      status: undefined,
      customerIds: ["org-2"],
      priorities: ["Low"],
    });
    expect(taskOptions().period.value).toEqual(localDayWindow(TODAY));
  });

  it("closes the day chip for every date", async () => {
    const wrapper = createWrapper();

    wrapper.getComponent(ChipsStub).vm.$emit("remove", chips(wrapper)[0]);
    await flushPromises();

    expect(taskOptions().period.value).toBeUndefined();
    expect(wrapper.getComponent(CalendarStub).props("modelValue")).toBeUndefined();
    expect(chips(wrapper)).toEqual([]);
  });

  // Reset empties the row, the day included; the search term keeps its own clear, as on the orders list.
  it("resets every chip, the day included, and keeps the search term", async () => {
    const wrapper = createWrapper();
    await wrapper.get(".search-input").setValue("renewal");
    await wrapper.get(".search").trigger("keydown", { key: "Enter" });
    await setFilters(wrapper, {
      keyword: "renewal",
      status: "overdue",
      customerIds: ["org-1"],
      priorities: ["High"],
    });

    wrapper.getComponent(ChipsStub).vm.$emit("reset");
    await settle();

    expect(taskOptions().filters.value).toEqual({ ...emptyTaskFilters(), keyword: "renewal" });
    expect(taskOptions().period.value).toBeUndefined();
  });
});

describe("Tasks page states", () => {
  // The page sat at the top of the hub with no trail back (QA A-20). Home is the composable's own.
  it("places itself in the account trail", () => {
    const wrapper = createWrapper();

    const titles = (wrapper.getComponent(BreadcrumbsStub).props("items") as { title: string }[]).map(
      (crumb) => crumb.title,
    );

    expect(titles).toEqual(["common.links.account", "sales_rep.hub.title", "sales_rep.tasks.title"]);
  });

  it("lists the tasks", () => {
    state.items.value = [makeTask()];

    const wrapper = createWrapper();

    expect(wrapper.getComponent(ListStub).props("tasks")).toHaveLength(1);
  });

  // Any filter or day replaces the list without a word (QA A-9), so the count is announced, atomically.
  it("announces the count of the whole list, not of the page on screen", () => {
    state.items.value = [makeTask()];
    state.totalCount.value = 4;

    const wrapper = createWrapper();

    const live = wrapper.get('[role="status"]');
    expect(live.attributes("aria-live")).toBe("polite");
    expect(live.attributes("aria-atomic")).toBe("true");
    expect(live.text()).toBe('sales_rep.tasks.day_task_count {"count":4}');
  });

  it("explains an empty day", () => {
    const wrapper = createWrapper();

    expect(wrapper.get(".empty").attributes("data-text")).toBe("sales_rep.tasks.empty_day");
    expect(wrapper.get(".empty").attributes("data-variant")).toBe("empty");
    expect(wrapper.findComponent(ListStub).exists()).toBe(false);
  });

  it("explains an empty list of every date", async () => {
    const wrapper = createWrapper();

    await pickDay(wrapper, undefined);

    expect(wrapper.get(".empty").attributes("data-text")).toBe("sales_rep.tasks.empty");
  });

  it("says when the filters match nothing", async () => {
    const wrapper = createWrapper();

    await setFilters(wrapper, { priorities: ["High"] });

    expect(wrapper.get(".empty").attributes("data-text")).toBe("sales_rep.tasks.no_results");
    expect(wrapper.get(".empty").attributes("data-variant")).toBe("search");
    // Reset search clears a search term; the filters have their own chips.
    expect(wrapper.get(".empty").find("button").exists()).toBe(false);
  });

  // The empty text describes the list on screen: a change still on its way to the query does not alter it yet.
  it("keeps the empty text of the list on screen until a change reaches it", async () => {
    const wrapper = createWrapper();
    await setFilters(wrapper, { priorities: ["High"] });

    wrapper.getComponent(ChipsStub).vm.$emit(
      "remove",
      chips(wrapper).find((chip) => chip.id === "priorities:High"),
    );
    await flushPromises();

    expect(wrapper.get(".empty").attributes("data-text")).toBe("sales_rep.tasks.no_results");

    await settle();

    expect(wrapper.get(".empty").attributes("data-text")).toBe("sales_rep.tasks.empty_day");
  });

  it("offers Reset search when a search term matches nothing", async () => {
    const wrapper = createWrapper();
    await wrapper.get(".search-input").setValue("renewal");
    await wrapper.get(".search").trigger("keydown", { key: "Enter" });
    await settle();

    await button(wrapper, "sales_rep.tasks.reset_search").trigger("click");
    await settle();

    expect(taskOptions().filters.value.keyword).toBe("");
  });

  // Apollo keeps the previous rows on a failed refetch, so the failure view has to win over them.
  it("replaces the rows with the failure view when the query failed but stale rows remain", () => {
    state.items.value = [makeTask()];
    state.error.value = new Error("boom");

    const wrapper = createWrapper();

    expect(wrapper.findComponent(ListStub).exists()).toBe(false);
    expect(wrapper.get(".empty").attributes("data-variant")).toBe("error");
  });

  it("explains each dot colour in a legend", () => {
    const wrapper = createWrapper();

    expect(wrapper.findAll(".sales-rep-tasks-page__legend-item").map((item) => item.text())).toEqual([
      "sales_rep.tasks.legend.upcoming",
      "sales_rep.tasks.legend.overdue",
      "sales_rep.tasks.legend.completed",
    ]);
  });

  // The rail is sized for the `sm` grid (VCST-6077); at `md` it would spill past the widget.
  it("draws the month rail's calendar at sm", () => {
    const wrapper = createWrapper();

    expect(wrapper.getComponent(CalendarStub).props("size")).toBe("sm");
  });

  it("names the month rail after its own heading", () => {
    const wrapper = createWrapper();
    const titleId = wrapper.get("aside").attributes("aria-labelledby");

    expect(titleId).toBeTruthy();
    expect(wrapper.get(`[id="${titleId}"]`).text()).toBe("sales_rep.tasks.due_dates");
  });
});

describe("Tasks page writes", () => {
  // Both surfaces read the same records, so a write refreshes the list and the dots.
  it("completes a task and refreshes the list and the dots", async () => {
    state.items.value = [makeTask()];

    const wrapper = createWrapper();
    wrapper.getComponent(ListStub).vm.$emit("toggle", makeTask());
    await flushPromises();

    expect(state.setCompleted).toHaveBeenCalledWith("task-1", true);
    expect(state.refetch).toHaveBeenCalled();
    expect(state.refetchMarkers).toHaveBeenCalled();
  });

  it("reopens a completed task", async () => {
    state.items.value = [makeTask({ status: "completed", isActive: false })];

    const wrapper = createWrapper();

    wrapper.getComponent(ListStub).vm.$emit("toggle", makeTask({ status: "completed", isActive: false }));
    await flushPromises();

    expect(state.setCompleted).toHaveBeenCalledWith("task-1", false);
  });

  // A failed toggle usually means the row is stale (deleted elsewhere); leaving it invites the same click.
  it("still refreshes when the write failed, so a stale row cannot linger", async () => {
    state.setCompleted.mockResolvedValue(false);
    state.items.value = [makeTask()];

    const wrapper = createWrapper();
    wrapper.getComponent(ListStub).vm.$emit("toggle", makeTask());
    await flushPromises();

    expect(state.refetch).toHaveBeenCalled();
    expect(state.refetchMarkers).toHaveBeenCalled();
  });

  it("creates against the day on screen", async () => {
    const wrapper = createWrapper();
    await pickDay(wrapper, "2026-10-20");

    await button(wrapper, "tasks.new_task").trigger("click");

    expect(lastModal().component).toBe(SalesRepTaskModal);
    expect(lastModal().props.defaultDay).toBe("2026-10-20");
    expect(lastModal().props.task).toBeUndefined();
  });

  it("creates against today when every date is on screen", async () => {
    const wrapper = createWrapper();
    await pickDay(wrapper, undefined);

    await button(wrapper, "tasks.new_task").trigger("click");

    expect(lastModal().props.defaultDay).toBe(TODAY);
  });

  it("edits the task the row asked for, and refreshes once it saved", async () => {
    state.items.value = [makeTask()];
    const wrapper = createWrapper();

    wrapper.getComponent(ListStub).vm.$emit("edit", makeTask());
    await flushPromises();

    expect(lastModal().props.task?.id).toBe("task-1");

    await lastModal().props.onSaved();

    expect(state.refetch).toHaveBeenCalled();
    expect(state.refetchMarkers).toHaveBeenCalled();
  });

  // Otherwise "Task saved" lands over a list the task is not in, and it looks like the save was lost.
  it("follows a task saved onto another day", async () => {
    const wrapper = createWrapper();
    await pickDay(wrapper, "2026-10-15");

    await button(wrapper, "tasks.new_task").trigger("click");
    await lastModal().props.onSaved("2026-10-20");

    expect(taskOptions().period.value).toEqual(localDayWindow("2026-10-20"));
    expect(state.setMonth).toHaveBeenCalledWith("2026-10-20");
  });

  // Every date holds the task wherever it moved, so the list stays as the rep set it.
  it("stays on every date when a task is saved with no day picked", async () => {
    const wrapper = createWrapper();
    await pickDay(wrapper, undefined);

    await button(wrapper, "tasks.new_task").trigger("click");
    await lastModal().props.onSaved("2026-10-20");

    expect(taskOptions().period.value).toBeUndefined();
    expect(state.refetch).toHaveBeenCalled();
    expect(state.refetchMarkers).toHaveBeenCalled();
  });

  /**
   * A query whose variables changed restarts on its own; refetching it as well would fire a second request with the
   * variables from before the move. So a move refetches only what it did not rescope.
   */
  it("leaves the surfaces the move rescoped to apollo, and refreshes only the rest", async () => {
    const wrapper = createWrapper();
    await pickDay(wrapper, "2026-10-15");

    await button(wrapper, "tasks.new_task").trigger("click");
    await lastModal().props.onSaved("2026-11-02");

    expect(state.refetch).not.toHaveBeenCalled();
    expect(state.refetchMarkers).not.toHaveBeenCalled();
  });

  it("still refreshes the grid when the save stayed inside the month on screen", async () => {
    const wrapper = createWrapper();
    await pickDay(wrapper, "2026-10-15");

    await button(wrapper, "tasks.new_task").trigger("click");
    await lastModal().props.onSaved("2026-10-20");

    expect(state.refetch).not.toHaveBeenCalled();
    expect(state.refetchMarkers).toHaveBeenCalled();
  });
});
