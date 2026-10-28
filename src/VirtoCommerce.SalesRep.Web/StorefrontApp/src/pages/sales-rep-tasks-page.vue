<template>
  <div class="sales-rep-tasks-page">
    <VcBreadcrumbs :items="breadcrumbs" />

    <div class="sales-rep-tasks-page__head">
      <div class="sales-rep-tasks-page__heading">
        <VcTypography class="sales-rep-tasks-page__title" tag="h1">
          {{ t("sales_rep.tasks.title") }}
        </VcTypography>

        <p class="sales-rep-tasks-page__subtitle">{{ t("sales_rep.tasks.page.subtitle") }}</p>
      </div>

      <div class="sales-rep-tasks-page__actions">
        <VcButton prepend-icon="plus" @click="openTaskModal()">
          {{ t("sales_rep.tasks.new_task") }}
        </VcButton>
      </div>
    </div>

    <SalesRepRuleAlert :filter-failed="filterRulesFailed" />

    <div class="sales-rep-tasks-page__toolbar">
      <VcInput
        v-model="localKeyword"
        maxlength="64"
        class="sales-rep-tasks-page__search-input"
        :disabled="loading"
        :placeholder="t('sales_rep.tasks.search_placeholder')"
        clearable
        @keydown.enter="applyKeyword"
        @clear="resetKeyword"
      >
        <template #append>
          <VcButton
            :aria-label="t('sales_rep.tasks.search_aria')"
            :disabled="loading"
            icon="search"
            icon-size="1.25rem"
            @click="applyKeyword"
          />
        </template>
      </VcInput>

      <SalesRepTaskFilters
        v-model="filters"
        :statuses="statusRules"
        :customers="customerOptions"
        :customers-failed="customersFailed"
      />
    </div>

    <!-- The day is one of the filters: a chip of its own, set from the calendar. -->
    <SalesRepFilterChips :chips="filterChips" @remove="removeFilterChip" @reset="resetFilters" />

    <div class="sales-rep-tasks-page__body">
      <div class="sales-rep-tasks-page__main">
        <VcWidget size="md">
          <template #header-container>
            <!-- Any filter or day replaces the list under it without a word (QA A-9), so its count is announced.
                 role="status" as well as the attributes: a bare div carrying only aria-live is exposed as a generic
                 node. The role implies polite+atomic, but the explicit pair is what older AT reads. -->
            <div class="sales-rep-tasks-page__count" role="status" aria-live="polite" aria-atomic="true">
              {{ t("sales_rep.tasks.day_task_count", { count: totalCount }, totalCount) }}
            </div>
          </template>

          <template #default-container>
            <!-- A failure replaces the rows: apollo keeps the previous ones on a failed refetch. -->
            <VcEmptyView v-if="failed && !loading" :text="t('sales_rep.tasks.load_failed')" variant="error" />

            <VcEmptyView
              v-else-if="!tasks.length && !loading"
              :text="emptyText"
              :variant="hasFilters ? 'search' : 'empty'"
              icon="calendar"
            >
              <template v-if="queryFilters.keyword" #button>
                <VcButton prepend-icon="reset" @click="resetKeyword">
                  {{ t("sales_rep.tasks.reset_search") }}
                </VcButton>
              </template>
            </VcEmptyView>

            <SalesRepTaskList
              v-else
              :tasks="tasks"
              :loading="loading"
              :busy="saving"
              :page="page"
              :pages="pages"
              @toggle="toggleCompletion"
              @edit="openTaskModal"
              @update:page="changePage"
            />
          </template>
        </VcWidget>
      </div>

      <aside class="sales-rep-tasks-page__aside" :aria-labelledby="dueDatesTitleId">
        <VcWidget size="sm">
          <!-- Centres the inline-flex calendar (QA M-10); the title and legend share its left edge. -->
          <div class="sales-rep-tasks-page__month">
            <!-- In the content, not the widget's title: a header row would add a divider the mockup does not have. -->
            <VcTypography :id="dueDatesTitleId" tag="h2" variant="h5" class="sales-rep-tasks-page__month-title">
              {{ t("sales_rep.tasks.due_dates") }}
            </VcTypography>

            <!-- Picks the day, or unpicks it for every date: its Clear, or a second click on the picked day. -->
            <SalesRepTaskCalendar
              v-model="day"
              size="sm"
              :month="month"
              :day-markers="dayMarkers"
              clearable
              @update:month="setMonth"
            />

            <ul class="sales-rep-tasks-page__legend">
              <li v-for="kind in TASK_MARKER_KINDS" :key="kind" class="sales-rep-tasks-page__legend-item">
                <span :class="`sales-rep-tasks-page__legend-dot sales-rep-tasks-page__legend-dot--${kind}`" />
                {{ t(`sales_rep.tasks.legend.${kind}`) }}
              </li>
            </ul>
          </div>
        </VcWidget>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, useId } from "vue";
import { useI18n } from "vue-i18n";
import { useBreadcrumbs, useModal, useRouteQueryParam } from "@vc-frontend/core";
import { refDebounced } from "@vueuse/core";
import SalesRepFilterChips from "../components/sales-rep-filter-chips.vue";
import SalesRepRuleAlert from "../components/sales-rep-rule-alert.vue";
import SalesRepTaskCalendar from "../components/sales-rep-task-calendar.vue";
import SalesRepTaskFilters from "../components/sales-rep-task-filters.vue";
import SalesRepTaskList from "../components/sales-rep-task-list.vue";
import SalesRepTaskModal from "../components/sales-rep-task-modal.vue";
import { useSalesRepCustomerOptions } from "../composables/useSalesRepCustomerOptions";
import { useSalesRepRuleSelection } from "../composables/useSalesRepRuleSelection";
import { useSalesRepRules } from "../composables/useSalesRepRules";
import { useMonthAnchor, useSalesRepTaskCalendar } from "../composables/useSalesRepTaskCalendar";
import { useSalesRepTaskMutations } from "../composables/useSalesRepTaskMutations";
import { useSalesRepTasks } from "../composables/useSalesRepTasks";
import { TASKS_FILTERS_DEBOUNCE_MS, TASKS_SORT_RULE } from "../constants";
import {
  emptyTaskFilters,
  localDayKey,
  localDayKeyToDate,
  localDayWindow,
  TASK_MARKER_KINDS,
  taskPriorityLabelKey,
  toMonthKey,
} from "../tasks";
import type { SalesRepFilterChipType } from "../types";
import type { SalesRepTaskFiltersType, SalesRepTaskType } from "../types/tasks";

const { t, d } = useI18n();
const { openModal } = useModal();

// Home is prepended by the composable. Same trail the customer pages carry, without their customer legs.
const breadcrumbs = useBreadcrumbs(() => [
  { title: t("common.links.account"), route: { name: "Account" } },
  { title: t("sales_rep.hub.title") },
  { title: t("sales_rep.tasks.title") },
]);

// Names the month rail's landmark after its own heading.
const dueDatesTitleId = useId();

// Resolved once, like the list's own boundary: a "today" that moved mid-session would reshuffle the list. So, on
// purpose, a page left open past midnight keeps calling the day it opened on "today" until it is reloaded.
const todayKey = localDayKey(new Date());

/**
 * The dashboard's overdue notice links here with `?filter=overdue`. Read once on the way in, then dropped: the filters
 * live in the page, and a parameter left in the URL would describe a list the rep has since changed. `replace`, so
 * Back does not lead to the same page with the parameter put back.
 */
const filterParam = useRouteQueryParam<string>("filter", { updateMethod: "replace" });
const linkedStatus = filterParam.value || undefined;
if (linkedStatus) {
  filterParam.value = "";
}

/**
 * The due day on screen, or undefined for every date. Opens on today, except from a status link: overdue work is never
 * due today, so that one opens on every date.
 */
const day = ref<string | undefined>(linkedStatus ? undefined : todayKey);
const filters = ref<SalesRepTaskFiltersType>({ ...emptyTaskFilters(), status: linkedStatus });

// Unapplied search term; committed to the filters on Enter or the search button.
const localKeyword = ref("");

const {
  rules: filterRules,
  failed: filterRulesFailed,
  loading: filterRulesLoading,
} = useSalesRepRules("task", "filter");

const selectedStatus = computed({
  get: () => filters.value.status,
  set: (status) => {
    filters.value = { ...filters.value, status };
  },
});

// A status the project does not offer (the dashboard links to "overdue" by name) falls back to every status, rather
// than filtering the list by something no select or chip shows.
const { selectableRules: statusRules } = useSalesRepRuleSelection(selectedStatus, filterRules, filterRulesLoading);

/**
 * The filters as the list and the dots ask them, a beat late: a multi-select stays open for the next pick, and every
 * pick would otherwise refetch both. The selects and chips follow each pick at once. Taken after the fallback above,
 * so a linked status that rules already at hand do not offer is never asked for.
 */
const queryFilters = refDebounced(filters, TASKS_FILTERS_DEBOUNCE_MS);

// Drives the dots query. The calendar owns which month is on screen and reports it back.
const { month, setMonth } = useMonthAnchor();

const period = computed(() => (day.value ? localDayWindow(day.value) : undefined));

const {
  items: tasks,
  loading,
  error,
  page,
  pages,
  totalCount,
  refetch,
} = useSalesRepTasks({
  period,
  filters: queryFilters,
  sort: TASKS_SORT_RULE,
});

// The dots follow the filters, but not the day: they are what a rep picks the day by.
const { dayMarkers, refetch: refetchMarkers } = useSalesRepTaskCalendar(month, queryFilters);
const { setCompleted, loading: saving } = useSalesRepTaskMutations();
const { options: customerOptions, failed: customersFailed, findOption } = useSalesRepCustomerOptions();

const failed = computed(() => Boolean(error.value));

// Read off what the list was asked, so its empty text never describes a refetch still on its way.
const hasFilters = computed(() => {
  const { keyword, status, customerIds, priorities } = queryFilters.value;
  return Boolean(keyword || status || customerIds.length || priorities.length);
});

const emptyText = computed(() => {
  if (hasFilters.value) {
    return t("sales_rep.tasks.no_results");
  }

  return t(day.value ? "sales_rep.tasks.empty_day" : "sales_rep.tasks.empty");
});

type TaskFilterChipType = SalesRepFilterChipType & {
  field: "day" | "status" | "customerIds" | "priorities";
  value?: string;
};

// In the mockup's order: the day, the customers, then the status and the priorities.
const filterChips = computed<TaskFilterChipType[]>(() => {
  const { status, customerIds, priorities } = filters.value;
  const chips: TaskFilterChipType[] = [];

  if (day.value) {
    // "short" (Sep 1, 2026), not "long" — the long named format appends a time, and the chip names a DAY.
    const label =
      day.value === todayKey
        ? t("sales_rep.tasks.filters.due_today")
        : t("sales_rep.tasks.due_relative.due", { date: d(localDayKeyToDate(day.value), "short") });

    chips.push({ id: "day", field: "day", label });
  }

  for (const customerId of customerIds) {
    chips.push({
      id: `customerIds:${customerId}`,
      field: "customerIds",
      value: customerId,
      label: findOption(customerId)?.organizationName ?? customerId,
    });
  }

  if (status) {
    chips.push({
      id: `status:${status}`,
      field: "status",
      value: status,
      label: filterRules.value.find((rule) => rule.name === status)?.label ?? status,
    });
  }

  for (const priority of priorities) {
    chips.push({
      id: `priorities:${priority}`,
      field: "priorities",
      value: priority,
      label: t(taskPriorityLabelKey(priority)),
    });
  }

  return chips;
});

function removeFilterChip(chip: TaskFilterChipType): void {
  switch (chip.field) {
    case "day":
      day.value = undefined;
      break;
    case "status":
      filters.value = { ...filters.value, status: undefined };
      break;
    case "customerIds":
      filters.value = { ...filters.value, customerIds: filters.value.customerIds.filter((id) => id !== chip.value) };
      break;
    case "priorities":
      filters.value = { ...filters.value, priorities: filters.value.priorities.filter((x) => x !== chip.value) };
      break;
  }
}

// Every chip at once, the day included. The search term keeps its own clear, as on the orders list.
function resetFilters(): void {
  day.value = undefined;
  filters.value = { ...emptyTaskFilters(), keyword: filters.value.keyword };
}

function applyKeyword(): void {
  filters.value = { ...filters.value, keyword: localKeyword.value.trim() };
}

function resetKeyword(): void {
  localKeyword.value = "";
  filters.value = { ...filters.value, keyword: "" };
}

function changePage(value: number): void {
  page.value = value;
}

/**
 * Both surfaces read the same records, so a write refreshes them rather than patching the cache.
 * allSettled, not all: a refetch that fails is already logged by its composable's onError and drawn by the
 * surface's failure view, so a rejection escaping here would only be console noise from an event handler.
 */
async function refreshAll(): Promise<void> {
  await Promise.allSettled([refetch(), refetchMarkers()]);
}

async function toggleCompletion(task: SalesRepTaskType): Promise<void> {
  await setCompleted(task.id, task.status !== "completed");

  // Either way: a failed toggle usually means the row is stale (deleted from another tab), and leaving it on
  // screen invites the same click again.
  await refreshAll();
}

// A save reports the day it landed on: a task moved to another date would otherwise be refetched into a list that no
// longer contains it, leaving "Task saved" over a list where it is nowhere to be seen. A delete reports nothing —
// there is no row left to go to.
async function onTaskSaved(dayKey?: string): Promise<void> {
  // Follow the task only while a day is picked, and only when it actually moved. Every date already holds it, as far
  // as the other filters let it in; those stay as the rep set them.
  const movedTo = dayKey && day.value && dayKey !== day.value ? dayKey : undefined;
  const rescopesGrid = !!movedTo && toMonthKey(movedTo) !== month.value;

  if (movedTo) {
    day.value = movedTo;
    setMonth(movedTo);
  }

  /**
   * Only the surfaces the move did NOT rescope get an explicit refetch. Apollo restarts a query whose variables
   * changed on its own, and its `restart` is deferred to `nextTick` while `refetch()` runs synchronously — so
   * refetching a rescoped query here fires a second, redundant request carrying the pre-move variables.
   */
  await Promise.allSettled([...(movedTo ? [] : [refetch()]), ...(rescopesGrid ? [] : [refetchMarkers()])]);
}

function openTaskModal(task?: SalesRepTaskType): void {
  openModal({
    component: SalesRepTaskModal,
    props: { task, defaultDay: day.value ?? todayKey, onSaved: onTaskSaved },
  });
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-tasks-page {
  &__head {
    @apply flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between;
  }

  &__title {
    @apply [word-break:break-word];
  }

  &__subtitle {
    @apply mt-1 text-sm text-neutral-500 [word-break:break-word];
  }

  &__actions {
    @apply flex flex-none flex-wrap gap-3;
  }

  // The search takes what the three selects leave; below lg they stack under it.
  &__toolbar {
    @apply flex flex-col gap-3 lg:flex-row lg:items-start;
  }

  &__search-input {
    @apply grow;
  }

  // The month rail splits off at xl, like layout-surface's aside, level with the list column.
  &__body {
    @apply flex flex-col gap-5 xl:flex-row xl:items-start;
  }

  &__main {
    @apply min-w-0 grow;
  }

  &__count {
    @apply px-6 py-4 text-sm text-neutral-600;
  }

  // Room for a `sm` calendar plus a long month name ("September 2026"); __month centres the rest.
  &__aside {
    @apply min-w-0 xl:w-[17.5rem] xl:shrink-0;
  }

  &__month {
    @apply mx-auto w-fit;
  }

  &__month-title {
    @apply mb-1;
  }

  &__legend {
    @apply m-0 mt-4 flex list-none flex-wrap gap-4 p-0 text-xs text-neutral-500;
  }

  &__legend-item {
    @apply flex items-center gap-1.5;
  }

  &__legend-dot {
    @apply size-1.5 rounded-full;

    &--upcoming {
      background-color: var(--color-info-500);
    }

    &--overdue {
      background-color: var(--color-danger-500);
    }

    // -600, matching the calendar dots: -500 shares a luminance with the blue, and -400 scored 2.69:1 on
    // white — under the 3:1 a meaningful graphic needs (QA A-11). Darker separates it on lightness too.
    &--completed {
      background-color: var(--color-success-600);
    }
  }
}
</style>
