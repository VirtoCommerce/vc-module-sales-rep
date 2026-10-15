<template>
  <VcModal
    ref="modalComponent"
    :title="title"
    class="sales-rep-task-modal"
    :is-persistent="loading"
    is-mobile-fullscreen
    dividers
  >
    <form class="sales-rep-task-modal__form" @submit.prevent>
      <VcInput
        v-model="name"
        :label="t('sales_rep.tasks.form.name_label')"
        :placeholder="t('common.placeholders.enter_value')"
        :disabled="loading"
        :maxlength="MAX_NAME_LENGTH"
        :message="meta.dirty ? errors.name : undefined"
        :error="!!errors.name && meta.dirty"
        :aria="REQUIRED_ARIA"
        required
      />

      <!-- Right under Title: it decides the fields below (a call adds Customer and Time). Absent in call mode, where
           the type is Call by definition, and when nobody has configured the TaskManagement.TaskTypes dictionary.
           `clearable` is bound rather than bare: VcSelect counts "" as a selection, so it would offer a Clear
           on an empty Type (QA M-2), and "" is this form's own convention for an unset field. -->
      <VcSelect
        v-if="!callMode && types.length"
        v-model="type"
        :label="t('sales_rep.tasks.form.type_label')"
        :items="types"
        :disabled="loading"
        :clearable="!!type"
        enable-teleport
      />

      <SalesRepTaskCustomerSelect
        v-if="isCall"
        v-model="customerId"
        :name="initial?.customerName"
        :disabled="loading"
      />

      <div :class="['sales-rep-task-modal__when', { 'sales-rep-task-modal__when--timed': isCall }]">
        <!-- enable-teleport: inside a modal the popover would otherwise clip against the dialog's overflow. -->
        <VcDatePicker
          v-model="dueDate"
          :label="t('sales_rep.tasks.form.due_date_label')"
          :disabled="loading"
          :message="meta.dirty ? errors.dueDate : undefined"
          :error="!!errors.dueDate && meta.dirty"
          :aria="REQUIRED_ARIA"
          enable-teleport
          required
        />

        <TimeSelect
          v-if="isCall"
          v-model="time"
          :label="t('sales_rep.tasks.form.time_label')"
          :step-minutes="CALL_TIME_STEP_MINUTES"
          :disabled="loading"
          required
        />
      </div>

      <VcSelect
        v-model="priority"
        :label="t('sales_rep.tasks.form.priority_label')"
        :items="priorityItems"
        text-field="label"
        value-field="value"
        :disabled="loading"
        enable-teleport
      />

      <VcTextarea
        v-model="description"
        :label="t('sales_rep.tasks.form.description_label')"
        :placeholder="t('common.placeholders.enter_value')"
        :disabled="loading"
        :max-length="MAX_DESCRIPTION_LENGTH"
        rows="4"
        counter
      />

      <!-- A new call only: the storefront cannot update an event it handed to a calendar, so editing offers none.
           VcLabel renders a <div> here, so the group takes its name through aria-labelledby. -->
      <fieldset v-if="offersCalendars" :aria-labelledby="calendarsLabelId">
        <VcLabel :id="calendarsLabelId" class="sales-rep-task-modal__calendars-label">
          {{ t("sales_rep.tasks.form.calendars_label") }}
        </VcLabel>

        <div class="sales-rep-task-modal__calendars">
          <VcCheckbox v-model="addToGoogleCalendar" :disabled="loading">
            {{ t("sales_rep.tasks.form.google_calendar") }}
          </VcCheckbox>

          <VcCheckbox v-model="addToAppleCalendar" :disabled="loading">
            {{ t("sales_rep.tasks.form.apple_calendar") }}
          </VcCheckbox>
        </div>
      </fieldset>
    </form>

    <template #actions="{ close }">
      <VcButton
        v-if="task"
        class="sales-rep-task-modal__delete"
        color="danger"
        variant="outline"
        :disabled="loading"
        @click="remove"
      >
        {{ t("sales_rep.tasks.form.delete_button") }}
      </VcButton>

      <VcButton color="secondary" variant="outline" :disabled="loading" @click="close">
        {{ t("sales_rep.tasks.form.cancel_button") }}
      </VcButton>

      <!-- Disabled while the form is invalid, by product decision. That is QA A-18 (advisory, not an AA
           failure): a disabled control leaves the tab order and cannot say why it is unavailable. The
           requirement itself is still conveyed - Title carries `required` and `aria-required`. -->
      <VcButton :disabled="!meta.valid" :loading="loading" @click="submit">
        {{ t("sales_rep.tasks.form.save_button") }}
      </VcButton>
    </template>
  </VcModal>
</template>

<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/yup";
import { useField, useForm } from "vee-validate";
import { computed, ref, useId, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { string } from "yup";
import { Logger, useModal, useNotifications, VcModal } from "@vc-frontend/core";
import { callEvent, googleCalendarUrl, ICS_FILE_NAME, ICS_FILE_TYPE, icsFile } from "../calendar";
import { useSalesRepTaskMutations } from "../composables/useSalesRepTaskMutations";
import { useSalesRepTaskTypes } from "../composables/useSalesRepTaskTypes";
import { CALL_DEFAULT_TIME, CALL_TIME_STEP_MINUTES } from "../constants";
import { openBlankTab, saveFile } from "../files";
import { dueDateForDay, dueDateForDayAndTime, isCallTask, localDayKey, localTimeKey } from "../tasks";
import SalesRepTaskCustomerSelect from "./sales-rep-task-customer-select.vue";
import TimeSelect from "./time-select.vue";
import type { SalesRepTaskFieldsFragment } from "../api/graphql/types";
import type { SalesRepTaskType } from "../types/tasks";

interface IProps {
  /** Omit to create. */
  task?: SalesRepTaskType;
  /** Pre-fills a new task: "Schedule call" passes a call for its customer. */
  draft?: Partial<SalesRepTaskType>;
  /** Pre-selects the due date when creating from a calendar cell ("YYYY-MM-DD"). */
  defaultDay?: string;
  /**
   * Lets the caller refetch; the modal owns no list. A save reports the day it wrote ("YYYY-MM-DD") so the
   * caller can follow a task that moved to another date; a delete reports nothing.
   */
  onSaved?: (dayKey?: string) => void;
}

const props = defineProps<IProps>();

// Mirrors the WorkTask.Name column width. Nothing server-side rejects a longer value today, so this input cap
// is what actually keeps a name inside the column.
const MAX_NAME_LENGTH = 256;

// Notes live in a `text` column, so this is a layout guard rather than a storage limit; 1000 matches the
// storefront's other free-text areas (order comment, invite message) rather than the 250 of a one-line list
// description. Deliberately NOT mirrored in the yup schema: `maxlength` stops the rep typing past it, while a
// validation rule would also invalidate a longer description written in the admin task UI and lock the rep
// out of editing that task at all.
const MAX_DESCRIPTION_LENGTH = 1000;

// `required` only draws VcLabel's asterisk; it reaches neither the control nor assistive tech, so the
// requirement was exposed by no route at all (QA A-7). Both VcInput and VcDatePicker merge an `aria` map
// onto the control they render, so this is fixable here without touching the ui-kit.
const REQUIRED_ARIA = { "aria-required": "true" };

const PRIORITIES = ["Lowest", "Low", "Normal", "High", "Highest"] as const;

const { t } = useI18n();
const notifications = useNotifications();
const { openModal } = useModal();
const modalComponent = useTemplateRef<InstanceType<typeof VcModal>>("modalComponent");

const { create, update, remove: removeTask, loading } = useSalesRepTaskMutations();
const { types } = useSalesRepTaskTypes();

const priorityItems = computed(() =>
  PRIORITIES.map((value) => ({ value, label: t(`sales_rep.tasks.priority.${value.toLowerCase()}`) })),
);

// The task being edited, or what a new one starts from. A deliberate snapshot: useForm seeds the fields once, and a
// modal instance is constructed per open with fixed props, so there is no later prop change to track.
const initial = props.task ?? props.draft;

// Call mode is fixed when the popup opens on a call (Schedule call, or editing a call): the type is not offered for
// change, so a call stays a call. A task popup can still become a call by picking that type.
const callMode = isCallTask(initial?.type);

const title = computed(() => {
  if (callMode) {
    return t(props.task ? "sales_rep.tasks.form.edit_call_title" : "sales_rep.tasks.form.create_call_title");
  }

  return t(props.task ? "sales_rep.tasks.form.edit_title" : "sales_rep.tasks.form.create_title");
});

/**
 * The picker speaks local "YYYY-MM-DD"; the API speaks instants (see tasks.ts). A task or draft shows its own date
 * or nothing - a task with no due date arrives from the admin UI, and seeding "today" here would hand it one it
 * never had; a drafted call leaves its day to the rep. Only a blank new task falls back to the selected day.
 */
function initialDueDate(): string {
  if (initial) {
    return initial.dueDate ? localDayKey(initial.dueDate) : "";
  }

  return props.defaultDay ?? localDayKey(new Date());
}

function buildInitialValues() {
  return {
    name: initial?.name ?? "",
    dueDate: initialDueDate(),
    // Only a call has a time of day worth showing; any other task is date-only, so becoming a call starts at 9:00.
    time: callMode && initial?.dueDate ? localTimeKey(initial.dueDate) : CALL_DEFAULT_TIME,
    priority: initial?.priority || "Normal",
    type: initial?.type ?? "",
    customerId: initial?.customerId ?? "",
    description: initial?.description ?? "",
  };
}

// validateOnMount so `meta.valid` is honest before the rep touches anything - Save is disabled on an empty
// title from the outset, rather than only after the field has been visited. The error TEXT is held back
// until the form is dirty, or a new task would open with a complaint already on it.
const { errors, meta, handleSubmit } = useForm({
  initialValues: buildInitialValues(),
  validateOnMount: true,
});

const { value: name } = useField<string>("name", toTypedSchema(string().trim().required().max(MAX_NAME_LENGTH)));
const { value: dueDate } = useField<string>("dueDate", toTypedSchema(string().required()));
const { value: time } = useField<string>("time");
const { value: priority } = useField<string>("priority");
const { value: type } = useField<string>("type");
const { value: customerId } = useField<string>("customerId");
const { value: description } = useField<string>("description");

// The type decides the fields: a call adds the time of day and the customer.
const isCall = computed(() => isCallTask(type.value));

// A new call can go to the rep's calendars; asked every time, never remembered.
const offersCalendars = computed(() => !props.task && isCall.value);
const addToGoogleCalendar = ref(false);
const addToAppleCalendar = ref(false);
const calendarsLabelId = useId();

// Google's page goes into a tab the Save click opened (see openBlankTab). It stays blank until the call is saved.
let googleTab: Window | null = null;

async function submit(): Promise<void> {
  googleTab = offersCalendars.value && addToGoogleCalendar.value ? openBlankTab() : null;

  try {
    await save();
  } finally {
    // Still blank: the call was not saved.
    googleTab?.close();
    googleTab = null;
  }
}

/**
 * Hands the saved call to the calendars the rep ticked, and says whether that went through. Never throws: the call is
 * saved either way, and the popup has to close rather than invite a second save.
 */
function addToCalendars(task: SalesRepTaskFieldsFragment): boolean {
  if (!offersCalendars.value) {
    return true;
  }

  try {
    const event = callEvent(task);

    if (addToAppleCalendar.value) {
      saveFile(icsFile(event), ICS_FILE_NAME, ICS_FILE_TYPE);
    }

    if (addToGoogleCalendar.value) {
      if (!googleTab) {
        // The browser blocked even the tab opened by the click.
        return false;
      }

      googleTab.location.href = googleCalendarUrl(event);
      googleTab = null;
    }

    return true;
  } catch (error) {
    Logger.error("[sales-rep] Adding the call to a calendar failed:", error);
    return false;
  }
}

const save = handleSubmit(async (data) => {
  const input = {
    name: data.name.trim(),
    // A call is due at the picked time; any other task keeps its time of day, see dueDateForDay.
    dueDate: isCall.value
      ? dueDateForDayAndTime(data.dueDate, data.time)
      : dueDateForDay(data.dueDate, props.task?.dueDate),
    priority: data.priority || undefined,
    type: data.type || undefined,
    // Customer is a call's field. Any other task sends back the link it came with (only the API sets one there),
    // because updateSalesRepTask replaces the task.
    customerId: isCall.value ? data.customerId || undefined : props.task?.customerId,
    description: data.description?.trim() || undefined,
  };

  const saved = props.task ? await update(props.task.id, input) : await create(input);
  if (!saved) {
    // useMutation keeps the global error toast for a failed write, so nothing extra to say here.
    return;
  }

  // A create returns the task as saved; an edit offers no calendars.
  if (saved === true || addToCalendars(saved)) {
    notifications.success({ text: t("sales_rep.tasks.form.saved"), duration: 5000, single: true });
  } else {
    notifications.warning({ text: t("sales_rep.tasks.form.calendar_failed"), duration: 5000, single: true });
  }

  props.onSaved?.(data.dueDate);
  modalComponent.value?.close();
});

function remove(): void {
  // Deleting a task is not undoable and the button sits in the same row as Save, so it asks first. A failed
  // delete leaves the confirmation open to retry, per this module's convention (see useSalesRepTaskMutations).
  const closeConfirmation = openModal({
    component: "VcConfirmationModal",
    props: {
      variant: "danger",
      loading,
      title: t("sales_rep.tasks.form.delete_title"),
      text: t("sales_rep.tasks.form.delete_confirm"),

      async onConfirm() {
        if (!props.task || !(await removeTask(props.task.id))) {
          return;
        }

        closeConfirmation();
        notifications.success({ text: t("sales_rep.tasks.form.deleted"), duration: 5000, single: true });
        props.onSaved?.();
        modalComponent.value?.close();
      },
    },
  });
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-task-modal {
  &__form {
    @apply space-y-4;
  }

  &__when--timed {
    @apply grid gap-4 sm:grid-cols-2;
  }

  &__calendars-label {
    @apply mb-2;
  }

  // One row; a long translation on a narrow screen wraps the second box.
  &__calendars {
    @apply flex flex-wrap gap-x-6 gap-y-2;
  }

  // Pushed away from the confirm pair so Delete is not a neighbour of Save.
  &__delete {
    @apply me-auto;
  }
}
</style>
