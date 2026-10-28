import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { dueDateForDayAndTime, localDayKey, localDayKeyToIso } from "../tasks";
import SalesRepTaskModal from "./sales-rep-task-modal.vue";
import type { SalesRepTaskType } from "../types/tasks";
import type { MockInstance } from "vitest";
import { Logger, VcButton } from "@vc-frontend/core";

const mutations = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return {
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    setCompleted: vi.fn(),
    loading: ref(false),
  };
});

const state = await vi.hoisted(async () => {
  const { ref } = await import("vue");
  return { types: ref<string[]>([]), success: vi.fn(), warning: vi.fn() };
});

// The real openBlankTab runs against a spied window.open; only the download is replaced.
const files = vi.hoisted(() => ({ saveFile: vi.fn() }));
vi.mock("../files", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  saveFile: files.saveFile,
}));

vi.mock("../composables/useSalesRepTaskMutations", () => ({
  useSalesRepTaskMutations: () => mutations,
}));
vi.mock("../composables/useSalesRepTaskTypes", async () => {
  const { ref } = await import("vue");
  return { useSalesRepTaskTypes: () => ({ types: state.types, loading: ref(false), error: ref(null) }) };
});
// Behind the call's Customer field, which renders only for a call.
vi.mock("../composables/useSalesRepCustomerOptions", async () => {
  const { ref } = await import("vue");
  const options = ref([
    { organizationId: "org-1", organizationName: "Lakeview Outfitters", location: "", imageUrl: "" },
  ]);
  return {
    useSalesRepCustomerOptions: () => ({
      options,
      failed: ref(false),
      findOption: (id: string) => options.value.find((option) => option.organizationId === id),
    }),
  };
});
const modal = vi.hoisted(() => ({ openModal: vi.fn(), closeModal: vi.fn(), closeConfirmation: vi.fn() }));
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useNotifications: () => ({ success: state.success, warning: state.warning, error: vi.fn() }),
  useModal: () => modal,
}));
vi.mock("vue-i18n", () => ({
  useI18n: () => ({ t: (key: string) => key, d: () => "Oct 15", n: String, locale: { value: "en-US" } }),
}));

const closeMock = vi.hoisted(() => vi.fn());

// Stands in for VcModal: renders both slots and exposes the close() the component calls through its ref.
const VcModalStub = {
  props: ["title"],
  methods: { close: closeMock },
  template: '<div class="modal" :data-title="title"><slot /><slot name="actions" :close="close" /></div>',
};

// One stub for every field: they differ only in which value they carry, and the label identifies them.
const FieldStub = {
  props: ["modelValue", "label", "items", "clearable", "maxLength", "aria"],
  emits: ["update:modelValue"],
  computed: {
    // Read through a computed so the template needs no nested quotes.
    ariaRequired(): unknown {
      return (this as unknown as { aria?: Record<string, unknown> }).aria?.["aria-required"];
    },
  },
  template:
    '<input class="field" :data-label="label" :value="modelValue"' +
    ' :data-clearable="String(!!clearable)" :data-max-length="maxLength"' +
    ' :data-aria-required="ariaRequired"' +
    " @input=\"$emit('update:modelValue', $event.target.value)\" />",
};

// A real checkbox under its label text, so a test ticks it the way the rep does.
const CheckboxStub = {
  props: ["modelValue", "disabled"],
  emits: ["update:modelValue"],
  template:
    '<label class="checkbox"><input type="checkbox" :checked="modelValue" :disabled="disabled"' +
    " @change=\"$emit('update:modelValue', $event.target.checked)\" /><slot /></label>",
};

// 9:30 on the rep's own clock, so the fixture lands on the same local day whatever timezone the suite runs in.
const TIMED_DUE_DATE = new Date(2026, 9, 15, 9, 30).toISOString();

// What createSalesRepTask answers: the task as saved, with its id and the customer's name.
const CREATED_TASK = {
  id: "task-new",
  name: "Call ACME",
  description: undefined,
  type: undefined,
  priority: "Normal",
  dueDate: localDayKeyToIso("2026-10-15"),
  isActive: true,
};

function makeTask(overrides: Partial<SalesRepTaskType> = {}): SalesRepTaskType {
  return {
    id: "task-1",
    name: "Call ACME about the renewal",
    description: "Ask about the Q4 order",
    type: "Finance",
    priority: "High",
    dueDate: localDayKeyToIso("2026-10-15"),
    isActive: true,
    completed: undefined,
    status: "upcoming",
    ...overrides,
  };
}

// Plain mount (not createWrapperFactory): this file mocks the vue-i18n module, and the shared factory's
// defaults build a real i18n plugin from it.
function createWrapper(props: Record<string, unknown> = {}) {
  return mount(SalesRepTaskModal, {
    props,
    global: {
      renderStubDefaultSlot: false,
      stubs: {
        VcModal: VcModalStub,
        VcInput: FieldStub,
        VcDatePicker: FieldStub,
        VcSelect: FieldStub,
        VcTextarea: FieldStub,
        VcCheckbox: CheckboxStub,
        VcLabel: { template: "<div><slot /></div>" },
        VcIcon: true,
      },
      // Real buttons: the actions are genuine <button>s the tests click, and Save reads its own disabled state.
      components: { VcButton },
    },
  });
}

type WrapperType = ReturnType<typeof createWrapper>;

const field = (wrapper: WrapperType, label: string) => wrapper.get(`[data-label$="${label}"]`);
const exists = (wrapper: WrapperType, label: string) => wrapper.find(`[data-label$="${label}"]`).exists();

function button(wrapper: WrapperType, key: string) {
  const found = wrapper.findAll("button").find((candidate) => candidate.text().includes(key));
  if (!found) {
    throw new Error(`No button for ${key}`);
  }
  return found;
}

async function save(wrapper: WrapperType) {
  await button(wrapper, "form.save_button").trigger("click");
  await flushPromises();
}

/** Delete asks first, so the test has to answer: click the button, then run the confirmation's onConfirm. */
async function confirmDelete(wrapper: WrapperType) {
  await wrapper.get(".sales-rep-task-modal__delete").trigger("click");

  const opened = modal.openModal.mock.calls.at(-1)?.[0] as {
    component: string;
    props: { onConfirm: () => Promise<void> };
  };
  expect(opened.component).toBe("VcConfirmationModal");

  await opened.props.onConfirm();
  await flushPromises();
}

beforeEach(() => {
  state.types.value = [];
  state.success.mockClear();
  state.warning.mockClear();
  files.saveFile.mockClear();
  closeMock.mockClear();
  mutations.loading.value = false;
  mutations.create.mockClear().mockResolvedValue(CREATED_TASK);
  mutations.update.mockClear().mockResolvedValue(true);
  mutations.remove.mockClear().mockResolvedValue(true);
  modal.closeConfirmation.mockClear();
  modal.openModal.mockClear().mockReturnValue(modal.closeConfirmation);
});

describe("SalesRepTaskModal create", () => {
  it("pre-selects the day the rep opened it from", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-20" });

    expect(field(wrapper, "due_date_label").attributes("value")).toBe("2026-10-20");
    // Nobody has to pick an importance to write a note down.
    expect(field(wrapper, "priority_label").attributes("value")).toBe("Normal");
  });

  it("sends the picked day as the instant the API stores, never the raw day key", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    await field(wrapper, "name_label").setValue("  Call ACME  ");

    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith({
      // Trimmed here as well as server-side: VcInput emits exactly what was typed.
      name: "Call ACME",
      dueDate: localDayKeyToIso("2026-10-15"),
      priority: "Normal",
      type: undefined,
      description: undefined,
    });
    expect(mutations.create.mock.calls[0][0].dueDate).not.toBe("2026-10-15");
  });

  it("confirms the write, tells the caller to refetch and closes", async () => {
    const onSaved = vi.fn();
    const wrapper = createWrapper({ defaultDay: "2026-10-15", onSaved });
    await field(wrapper, "name_label").setValue("Call ACME");

    await save(wrapper);

    expect(state.success).toHaveBeenCalled();
    expect(onSaved).toHaveBeenCalled();
    expect(closeMock).toHaveBeenCalled();
  });

  // The caller shows one day at a time, so a task written to another one has to say where it went.
  it("reports the day it wrote, not the day it opened on", async () => {
    const onSaved = vi.fn();
    const wrapper = createWrapper({ defaultDay: "2026-10-15", onSaved });
    await field(wrapper, "name_label").setValue("Call ACME");
    await field(wrapper, "due_date_label").setValue("2026-11-02");

    await save(wrapper);

    expect(onSaved).toHaveBeenCalledWith("2026-11-02");
  });

  // useMutation already raises the global error toast; what matters here is that the rep does not lose the form.
  it("keeps the form open when the write failed", async () => {
    const onSaved = vi.fn();
    mutations.create.mockResolvedValue(undefined);
    const wrapper = createWrapper({ defaultDay: "2026-10-15", onSaved });
    await field(wrapper, "name_label").setValue("Call ACME");

    await save(wrapper);

    expect(state.success).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
    expect(closeMock).not.toHaveBeenCalled();
  });

  it("does not write a task with no title", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    await save(wrapper);

    expect(mutations.create).not.toHaveBeenCalled();
    expect(closeMock).not.toHaveBeenCalled();
  });

  it("offers no Delete before there is anything to delete", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(wrapper.find(".sales-rep-task-modal__delete").exists()).toBe(false);
  });
});

describe("SalesRepTaskModal edit", () => {
  it("seeds every field from the task, with the due date back on the rep's calendar", () => {
    const wrapper = createWrapper({ task: makeTask() });

    expect(wrapper.get(".modal").attributes("data-title")).toBe("sales_rep.tasks.form.edit_title");
    expect(field(wrapper, "name_label").attributes("value")).toBe("Call ACME about the renewal");
    expect(field(wrapper, "due_date_label").attributes("value")).toBe("2026-10-15");
    expect(field(wrapper, "priority_label").attributes("value")).toBe("High");
    expect(field(wrapper, "description_label").attributes("value")).toBe("Ask about the Q4 order");
  });

  it("updates the same task rather than creating another", async () => {
    const wrapper = createWrapper({ task: makeTask() });
    await field(wrapper, "name_label").setValue("Call ACME back");

    await save(wrapper);

    expect(mutations.create).not.toHaveBeenCalled();
    expect(mutations.update).toHaveBeenCalledWith("task-1", expect.objectContaining({ name: "Call ACME back" }));
  });

  // A task can carry a real time of day — the admin task UI sets one — while this form is date-only and
  // updateSalesRepTask REPLACES the task. Editing the title must not reset the deadline (VCST-5732 QA A-1).
  it("leaves the deadline exactly as it found it when only the title changed", async () => {
    const wrapper = createWrapper({ task: makeTask({ dueDate: TIMED_DUE_DATE }) });
    await field(wrapper, "name_label").setValue("Call ACME back");

    await save(wrapper);

    expect(mutations.update.mock.calls[0][1].dueDate).toBe(TIMED_DUE_DATE);
  });

  it("keeps the hour it was scheduled for when the rep moves it to another day", async () => {
    const wrapper = createWrapper({ task: makeTask({ dueDate: TIMED_DUE_DATE }) });
    await field(wrapper, "due_date_label").setValue("2026-11-02");

    await save(wrapper);

    const sent = new Date(mutations.update.mock.calls[0][1].dueDate as string);
    expect(localDayKey(sent)).toBe("2026-11-02");
    expect([sent.getHours(), sent.getMinutes()]).toEqual([9, 30]);
  });

  // Deleting is not undoable and the button shares a row with Save, so the click alone must not do it.
  it("asks before deleting anything", async () => {
    const wrapper = createWrapper({ task: makeTask() });

    await wrapper.get(".sales-rep-task-modal__delete").trigger("click");
    await flushPromises();

    expect(modal.openModal).toHaveBeenCalled();
    expect(mutations.remove).not.toHaveBeenCalled();
  });

  it("deletes once confirmed, then tells the caller to refetch and closes", async () => {
    const onSaved = vi.fn();
    const wrapper = createWrapper({ task: makeTask(), onSaved });

    await confirmDelete(wrapper);

    expect(mutations.remove).toHaveBeenCalledWith("task-1");
    // No day: a delete leaves no row for the caller to navigate to.
    expect(onSaved).toHaveBeenCalledWith();
    expect(modal.closeConfirmation).toHaveBeenCalled();
    expect(closeMock).toHaveBeenCalled();
  });

  it("keeps both dialogs open when the delete failed", async () => {
    mutations.remove.mockResolvedValue(false);
    const wrapper = createWrapper({ task: makeTask() });

    await confirmDelete(wrapper);

    expect(modal.closeConfirmation).not.toHaveBeenCalled();
    expect(closeMock).not.toHaveBeenCalled();
  });
});

describe("SalesRepTaskModal accessibility", () => {
  // `required` only draws VcLabel's asterisk — it reaches neither the control nor assistive tech, so the
  // requirement was exposed by no route at all (QA A-7).
  it("marks the required controls as required, not just with an asterisk", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(field(wrapper, "name_label").attributes("data-aria-required")).toBe("true");
    expect(field(wrapper, "due_date_label").attributes("data-aria-required")).toBe("true");
  });

  // Product decision over QA A-18 (advisory): Save is disabled until the form is valid. The requirement is
  // still conveyed by Title's `required` and `aria-required`, which is the part that was a real failure.
  it("disables Save until the form is valid", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    // validateOnMount settles a tick after mount; without this the flag is still at its optimistic default.
    await flushPromises();

    expect(button(wrapper, "form.save_button").attributes("disabled")).toBeDefined();

    await field(wrapper, "name_label").setValue("Call ACME");
    await flushPromises();

    expect(button(wrapper, "form.save_button").attributes("disabled")).toBeUndefined();
  });
});

describe("SalesRepTaskModal length limits", () => {
  // Unbounded Notes was half of QA M-1. `text` column, so this is a layout guard, not a storage limit.
  it("caps what the rep can type into Notes, and shows how much is left", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(field(wrapper, "description_label").attributes("data-max-length")).toBe("1000");
  });

  // Deliberately NOT a yup rule: a description written in the admin task UI can be longer, and validating it
  // here would disable Save and lock the rep out of editing that task at all — the A-1 mistake, again.
  it("still saves a task whose description already exceeds the cap", async () => {
    const overlong = "x".repeat(1500);
    const wrapper = createWrapper({ task: makeTask({ description: overlong }) });
    await field(wrapper, "name_label").setValue("Call ACME back");

    await save(wrapper);

    expect(mutations.update).toHaveBeenCalledWith("task-1", expect.objectContaining({ description: overlong }));
  });
});

describe("SalesRepTaskModal type vocabulary", () => {
  // The shipped TaskManagement.TaskTypes defaults are back-office flavoured, so a deployment that never
  // configured them should not show a select with nothing worth choosing.
  it("hides the type field when nobody has configured the dictionary", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(exists(wrapper, "type_label")).toBe(false);
  });

  it("offers the type field once the dictionary has values", async () => {
    state.types.value = ["Finance", "Follow-up"];
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    await field(wrapper, "name_label").setValue("Call ACME");
    await field(wrapper, "type_label").setValue("Follow-up");

    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith(expect.objectContaining({ type: "Follow-up" }));
  });

  // VcSelect reads any non-nullish model as a selection, and this form seeds "" — so a bare `clearable`
  // offered a Clear button on a Type that had nothing to clear (QA M-2).
  it("offers Clear only once a type has been picked", async () => {
    state.types.value = ["Finance", "Follow-up"];
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(field(wrapper, "type_label").attributes("data-clearable")).toBe("false");

    await field(wrapper, "type_label").setValue("Follow-up");

    expect(field(wrapper, "type_label").attributes("data-clearable")).toBe("true");
  });

  // Editing a task that already carries one: the button has to be there from the first render.
  it("offers Clear straight away on a task that already has a type", () => {
    state.types.value = ["Finance", "Follow-up"];
    const wrapper = createWrapper({ task: makeTask() });

    expect(field(wrapper, "type_label").attributes("data-clearable")).toBe("true");
  });
});

// VCST-6314: a task of the Call type gets a time of day and a customer; "Schedule call" opens a drafted one.
describe("SalesRepTaskModal calls", () => {
  const CALL_DRAFT = { name: "Call", type: "Call", customerId: "org-1", customerName: "Lakeview Outfitters" };

  // The backend keeps Call in the dictionary, so the type list always offers it.
  beforeEach(() => {
    state.types.value = ["Other", "Call"];
  });

  it("opens a drafted call as a call for that customer: titled, at 9:00, on a day still to pick", () => {
    const wrapper = createWrapper({ draft: CALL_DRAFT });

    expect(wrapper.get(".modal").attributes("data-title")).toBe("sales_rep.tasks.form.create_call_title");
    expect(field(wrapper, "name_label").attributes("value")).toBe("Call");
    expect(field(wrapper, "customer_label").attributes("value")).toBe("org-1");
    expect(field(wrapper, "due_date_label").attributes("value")).toBe("");
    expect(field(wrapper, "time_label").attributes("value")).toBe("09:00");
    expect(field(wrapper, "priority_label").attributes("value")).toBe("Normal");
  });

  it("will not save a call before its day is picked", async () => {
    const wrapper = createWrapper({ draft: CALL_DRAFT });
    await flushPromises();

    expect(button(wrapper, "form.save_button").attributes("disabled")).toBeDefined();

    await field(wrapper, "due_date_label").setValue("2026-10-20");
    await flushPromises();

    expect(button(wrapper, "form.save_button").attributes("disabled")).toBeUndefined();
  });

  it("writes the picked day at the picked time, as a call linked to the customer", async () => {
    const wrapper = createWrapper({ draft: CALL_DRAFT });
    await field(wrapper, "due_date_label").setValue("2026-10-20");
    await field(wrapper, "time_label").setValue("14:45");

    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith(
      expect.objectContaining({
        name: "Call",
        type: "Call",
        customerId: "org-1",
        dueDate: dueDateForDayAndTime("2026-10-20", "14:45"),
      }),
    );
  });

  it("reopens a call at its own time, and moves it when the rep picks another", async () => {
    const wrapper = createWrapper({
      task: makeTask({
        type: "Call",
        dueDate: TIMED_DUE_DATE,
        customerId: "org-1",
        customerName: "Lakeview Outfitters",
      }),
    });

    expect(wrapper.get(".modal").attributes("data-title")).toBe("sales_rep.tasks.form.edit_call_title");
    expect(field(wrapper, "time_label").attributes("value")).toBe("09:30");
    expect(field(wrapper, "customer_label").attributes("value")).toBe("org-1");

    await field(wrapper, "time_label").setValue("11:00");
    await save(wrapper);

    const sent = new Date(mutations.update.mock.calls[0][1].dueDate as string);
    expect(localDayKey(sent)).toBe("2026-10-15");
    expect([sent.getHours(), sent.getMinutes()]).toEqual([11, 0]);
  });

  // updateSalesRepTask replaces the task, so an edit that moves neither the day nor the time sends the instant back as
  // it was, seconds and all - as a date-only task's edit already does (VCST-5732 QA A-1).
  it("leaves an unchanged call's instant exactly as it found it", async () => {
    const dueDate = new Date(2026, 9, 15, 9, 30, 15, 250).toISOString();
    const wrapper = createWrapper({
      task: makeTask({ type: "Call", dueDate, customerId: "org-1", customerName: "Lakeview Outfitters" }),
    });
    await field(wrapper, "name_label").setValue("Call ACME back");

    await save(wrapper);

    expect(mutations.update.mock.calls[0][1].dueDate).toBe(dueDate);
  });

  // Schedule call must not turn into some other task, and a call opened from the list stays one: the type is not
  // offered for change in either.
  it("offers no Type in call mode", () => {
    expect(exists(createWrapper({ draft: CALL_DRAFT }), "type_label")).toBe(false);
    expect(exists(createWrapper({ task: makeTask({ type: "Call", customerId: "org-1" }) }), "type_label")).toBe(false);
  });

  // The type decides which fields follow, so it sits right under the title instead of at the bottom.
  it("puts Type right under Title, then Customer, above the date", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    const labels = wrapper.findAll("[data-label]").map((input) => input.attributes("data-label"));
    expect(labels.slice(0, 4)).toEqual([
      "sales_rep.tasks.form.name_label",
      "sales_rep.tasks.form.type_label",
      "sales_rep.tasks.form.customer_label",
      "sales_rep.tasks.form.due_date_label",
    ]);
  });

  // Any task can be about a customer (VCST-6157); only a call is due at a time of day.
  it("asks every task for a customer, and a task for a time only once it is a call", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(exists(wrapper, "customer_label")).toBe(true);
    expect(exists(wrapper, "time_label")).toBe(false);

    await field(wrapper, "type_label").setValue("Call");

    expect(exists(wrapper, "time_label")).toBe(true);
  });

  // A non-call task is date-only, so its midnight is no time anybody picked; becoming a call starts at 9:00.
  it("starts a task that becomes a call at 9:00", async () => {
    const wrapper = createWrapper({ task: makeTask({ type: "Other" }) });

    await field(wrapper, "type_label").setValue("Call");

    expect(field(wrapper, "time_label").attributes("value")).toBe("09:00");
  });

  it("keeps the customer picked for a call when the task stops being one", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    await field(wrapper, "name_label").setValue("Follow up");
    await field(wrapper, "type_label").setValue("Call");
    await field(wrapper, "customer_label").setValue("org-1");

    await field(wrapper, "type_label").setValue("Other");
    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith(expect.objectContaining({ type: "Other", customerId: "org-1" }));
  });

  it("links a customer to a task that is not a call", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    await field(wrapper, "name_label").setValue("Send the price list");
    await field(wrapper, "customer_label").setValue("org-1");

    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith(expect.objectContaining({ type: undefined, customerId: "org-1" }));
  });

  it("shows the customer of a task that is not a call, and sends it back as it came", async () => {
    const wrapper = createWrapper({ task: makeTask({ customerId: "org-1", customerName: "Lakeview Outfitters" }) });

    expect(field(wrapper, "customer_label").attributes("value")).toBe("org-1");

    await save(wrapper);

    expect(mutations.update).toHaveBeenCalledWith("task-1", expect.objectContaining({ customerId: "org-1" }));
  });

  // updateSalesRepTask replaces the task, so no customer is what unlinks one.
  it("unlinks the customer the rep cleared", async () => {
    const wrapper = createWrapper({ task: makeTask({ customerId: "org-1", customerName: "Lakeview Outfitters" }) });

    await field(wrapper, "customer_label").setValue("");
    await save(wrapper);

    expect(mutations.update).toHaveBeenCalledWith("task-1", expect.objectContaining({ customerId: undefined }));
  });
});

// VCST-6314: a new call can go to the rep's Google and Apple calendars. Each event is a copy the storefront cannot
// update later, so editing offers neither.
describe("SalesRepTaskModal calendars", () => {
  const CALL_DRAFT = { name: "Call", type: "Call", customerId: "org-1", customerName: "Lakeview Outfitters" };
  const CREATED_CALL = {
    ...CREATED_TASK,
    name: "Call",
    type: "Call",
    dueDate: "2026-10-20T13:00:00.000Z",
    customerId: "org-1",
    customerName: "Lakeview Outfitters",
  };

  let openSpy: MockInstance<typeof window.open>;
  let googleTab: { location: { href: string }; opener: unknown; close: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    state.types.value = ["Other", "Call"];
    mutations.create.mockResolvedValue(CREATED_CALL);
    googleTab = { location: { href: "" }, opener: {}, close: vi.fn() };
    openSpy = vi.spyOn(window, "open").mockReturnValue(googleTab as unknown as Window);
  });

  afterEach(() => {
    openSpy.mockRestore();
  });

  const calendar = (wrapper: WrapperType, key: string) =>
    wrapper.findAll(".checkbox").find((candidate) => candidate.text().includes(key));

  async function tick(wrapper: WrapperType, key: string) {
    await calendar(wrapper, key)!.get("input").setValue(true);
  }

  /** A drafted call, ready to save: its day is the one thing Schedule call leaves to the rep. */
  async function scheduleCall() {
    const wrapper = createWrapper({ draft: CALL_DRAFT });
    await field(wrapper, "due_date_label").setValue("2026-10-20");
    await flushPromises();
    return wrapper;
  }

  it("offers both calendars, unticked, when scheduling a call", async () => {
    const wrapper = await scheduleCall();

    expect(calendar(wrapper, "google_calendar")!.get("input").element.checked).toBe(false);
    expect(calendar(wrapper, "apple_calendar")!.get("input").element.checked).toBe(false);
  });

  it("names the group for assistive tech", async () => {
    const wrapper = await scheduleCall();

    const labelId = wrapper.get("fieldset").attributes("aria-labelledby");
    expect(wrapper.get(`[id="${labelId}"]`).text()).toBe("sales_rep.tasks.form.calendars_label");
  });

  it("offers them on a new task only once it is a call", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(calendar(wrapper, "google_calendar")).toBeUndefined();

    await field(wrapper, "type_label").setValue("Call");

    expect(calendar(wrapper, "google_calendar")).toBeDefined();
    expect(calendar(wrapper, "apple_calendar")).toBeDefined();
  });

  it("offers none when editing a call", () => {
    const wrapper = createWrapper({ task: makeTask({ type: "Call", dueDate: TIMED_DUE_DATE, customerId: "org-1" }) });

    expect(wrapper.find("fieldset").exists()).toBe(false);
  });

  it("adds the call nowhere when nothing is ticked", async () => {
    const wrapper = await scheduleCall();

    await save(wrapper);

    expect(mutations.create).toHaveBeenCalled();
    expect(openSpy).not.toHaveBeenCalled();
    expect(files.saveFile).not.toHaveBeenCalled();
    expect(state.success).toHaveBeenCalled();
  });

  // A tab opened after the save's `await` is popup-blocked, so the click opens it and the saved call fills it in.
  it("opens Google's tab in the Save click, then fills in the call as the API saved it", async () => {
    let resolveCreate!: (task: typeof CREATED_CALL) => void;
    mutations.create.mockReturnValue(new Promise((resolve) => (resolveCreate = resolve)));
    const wrapper = await scheduleCall();
    await tick(wrapper, "google_calendar");

    await button(wrapper, "form.save_button").trigger("click");

    expect(openSpy).toHaveBeenCalledWith("", "_blank");
    expect(googleTab.location.href).toBe("");

    resolveCreate(CREATED_CALL);
    await flushPromises();

    const url = new URL(googleTab.location.href);
    expect(url.hostname).toBe("calendar.google.com");
    expect(url.searchParams.get("text")).toBe("Call — Lakeview Outfitters");
    expect(url.searchParams.get("dates")).toBe("20261020T130000Z/20261020T133000Z");
    expect(googleTab.close).not.toHaveBeenCalled();
    expect(state.success).toHaveBeenCalled();
    expect(closeMock).toHaveBeenCalled();
  });

  it("closes Google's tab, still blank, and keeps the form when the call was not saved", async () => {
    mutations.create.mockResolvedValue(undefined);
    const wrapper = await scheduleCall();
    await tick(wrapper, "google_calendar");

    await save(wrapper);

    expect(googleTab.close).toHaveBeenCalled();
    expect(googleTab.location.href).toBe("");
    expect(closeMock).not.toHaveBeenCalled();
  });

  it("downloads the call as an .ics file for Apple Calendar", async () => {
    const wrapper = await scheduleCall();
    await tick(wrapper, "apple_calendar");

    await save(wrapper);

    expect(files.saveFile).toHaveBeenCalledWith(
      expect.stringContaining("SUMMARY:Call — Lakeview Outfitters"),
      "call.ics",
      "text/calendar;charset=utf-8",
    );
    expect(files.saveFile.mock.calls[0][0]).toContain(`UID:${CREATED_CALL.id}`);
    expect(openSpy).not.toHaveBeenCalled();
    expect(closeMock).toHaveBeenCalled();
  });

  // The call is saved by then, so the form closes either way: left open, it would invite a second save.
  it("warns, and still closes, when the browser blocked even the tab the click opened", async () => {
    openSpy.mockReturnValue(null);
    const wrapper = await scheduleCall();
    await tick(wrapper, "google_calendar");

    await save(wrapper);

    expect(mutations.create).toHaveBeenCalled();
    expect(state.warning).toHaveBeenCalledWith(
      expect.objectContaining({ text: "sales_rep.tasks.form.calendar_failed" }),
    );
    expect(state.success).not.toHaveBeenCalled();
    expect(closeMock).toHaveBeenCalled();
  });

  it("warns, logs and still closes when the calendar step throws", async () => {
    const loggerSpy = vi.spyOn(Logger, "error").mockImplementation(() => {});
    files.saveFile.mockImplementationOnce(() => {
      throw new Error("download refused");
    });
    const wrapper = await scheduleCall();
    await tick(wrapper, "apple_calendar");

    await save(wrapper);

    expect(state.warning).toHaveBeenCalledWith(
      expect.objectContaining({ text: "sales_rep.tasks.form.calendar_failed" }),
    );
    expect(loggerSpy).toHaveBeenCalled();
    expect(closeMock).toHaveBeenCalled();
    loggerSpy.mockRestore();
  });

  it("adds nothing once the task stops being a call", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    await field(wrapper, "name_label").setValue("Follow up");
    await field(wrapper, "type_label").setValue("Call");
    await tick(wrapper, "google_calendar");
    await tick(wrapper, "apple_calendar");

    await field(wrapper, "type_label").setValue("Other");
    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith(expect.objectContaining({ type: "Other" }));
    expect(openSpy).not.toHaveBeenCalled();
    expect(files.saveFile).not.toHaveBeenCalled();
  });
});
