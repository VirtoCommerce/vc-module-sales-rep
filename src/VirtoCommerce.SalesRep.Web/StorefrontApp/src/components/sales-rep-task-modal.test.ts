import { flushPromises, mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { dueDateForDayAndTime, localDayKey, localDayKeyToIso } from "../tasks";
import SalesRepTaskModal from "./sales-rep-task-modal.vue";
import type { SalesRepTaskType } from "../types/tasks";
import { VcButton } from "@vc-frontend/core";

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
  return { types: ref<string[]>([]), success: vi.fn() };
});

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
  useNotifications: () => ({ success: state.success, error: vi.fn() }),
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

// 9:30 on the rep's own clock, so the fixture lands on the same local day whatever timezone the suite runs in.
const TIMED_DUE_DATE = new Date(2026, 9, 15, 9, 30).toISOString();

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
  closeMock.mockClear();
  mutations.loading.value = false;
  mutations.create.mockClear().mockResolvedValue(true);
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
    mutations.create.mockResolvedValue(false);
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

  // Schedule call must not turn into some other task, and a call opened from the list stays one: the type is not
  // offered for change in either.
  it("offers no Type in call mode", () => {
    expect(exists(createWrapper({ draft: CALL_DRAFT }), "type_label")).toBe(false);
    expect(exists(createWrapper({ task: makeTask({ type: "Call", customerId: "org-1" }) }), "type_label")).toBe(false);
  });

  // The type decides which fields follow, so it sits right under the title instead of at the bottom.
  it("puts Type right under Title, above the fields it controls", () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    const labels = wrapper.findAll("[data-label]").map((input) => input.attributes("data-label"));
    expect(labels.slice(0, 3)).toEqual([
      "sales_rep.tasks.form.name_label",
      "sales_rep.tasks.form.type_label",
      "sales_rep.tasks.form.due_date_label",
    ]);
  });

  it("asks a task for a time and a customer only once it is a call", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });

    expect(exists(wrapper, "time_label")).toBe(false);
    expect(exists(wrapper, "customer_label")).toBe(false);

    await field(wrapper, "type_label").setValue("Call");

    expect(exists(wrapper, "time_label")).toBe(true);
    expect(exists(wrapper, "customer_label")).toBe(true);
  });

  // A non-call task is date-only, so its midnight is no time anybody picked; becoming a call starts at 9:00.
  it("starts a task that becomes a call at 9:00", async () => {
    const wrapper = createWrapper({ task: makeTask({ type: "Other" }) });

    await field(wrapper, "type_label").setValue("Call");

    expect(field(wrapper, "time_label").attributes("value")).toBe("09:00");
  });

  it("drops the customer picked for a call when the task stops being one", async () => {
    const wrapper = createWrapper({ defaultDay: "2026-10-15" });
    await field(wrapper, "name_label").setValue("Follow up");
    await field(wrapper, "type_label").setValue("Call");
    await field(wrapper, "customer_label").setValue("org-1");

    await field(wrapper, "type_label").setValue("Other");
    await save(wrapper);

    expect(mutations.create).toHaveBeenCalledWith(expect.objectContaining({ type: "Other", customerId: undefined }));
  });

  // Customer is a call's field, but the API can link one to any task. Such a link is not shown, and is still sent
  // back as it came, because updateSalesRepTask replaces the task.
  it("keeps the unshown customer of a task that is not a call", async () => {
    const wrapper = createWrapper({ task: makeTask({ customerId: "org-1", customerName: "Lakeview Outfitters" }) });

    expect(exists(wrapper, "customer_label")).toBe(false);

    await save(wrapper);

    expect(mutations.update).toHaveBeenCalledWith("task-1", expect.objectContaining({ customerId: "org-1" }));
  });
});
