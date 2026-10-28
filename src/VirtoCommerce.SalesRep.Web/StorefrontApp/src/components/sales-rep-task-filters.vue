<template>
  <div class="sales-rep-task-filters">
    <!-- In this kit `multiple` holds whole items, not values (vc-frontend#2482 changes that), so the ids are mapped
         to items and back here and the page deals in ids alone. Searched in the browser over the first customers the
         options query brings (see useSalesRepCustomerOptions). -->
    <VcSelect
      :model-value="selectedCustomers"
      class="sales-rep-task-filters__select sales-rep-task-filters__select--multiple"
      :items="customerOptions"
      text-field="organizationName"
      value-field="organizationId"
      :aria-label="t('sales_rep.tasks.filters.customers_label')"
      :placeholder="t('sales_rep.tasks.filters.all_customers')"
      :error="customersFailed"
      :message="customersFailed ? t('sales_rep.tasks.form.customer_error') : undefined"
      multiple
      autocomplete
      clearable
      lazy
      @change="changeCustomers"
    />

    <!-- `clearable` is bound: VcSelect counts any defined value as a selection and would offer a Clear on none. -->
    <VcSelect
      :model-value="modelValue.status"
      class="sales-rep-task-filters__select"
      :items="statuses"
      text-field="label"
      value-field="name"
      :aria-label="t('sales_rep.tasks.filters.status_label')"
      :placeholder="t('sales_rep.tasks.filters.all_statuses')"
      :clearable="!!modelValue.status"
      @change="changeStatus"
    />

    <VcSelect
      :model-value="selectedPriorities"
      class="sales-rep-task-filters__select sales-rep-task-filters__select--multiple"
      :items="priorityItems"
      text-field="label"
      value-field="value"
      :aria-label="t('sales_rep.tasks.filters.priorities_label')"
      :placeholder="t('sales_rep.tasks.filters.all_priorities')"
      multiple
      clearable
      @change="changePriorities"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useSalesRepCustomerOptions } from "../composables/useSalesRepCustomerOptions";
import { TASK_PRIORITIES, taskPriorityLabelKey } from "../tasks";
import type { SalesRepCustomerOptionType } from "../composables/useSalesRepCustomerOptions";
import type { SalesRepRuleType } from "../types";
import type { SalesRepTaskFiltersType } from "../types/tasks";

interface IProps {
  /** The status rules to offer, from salesRepTaskFilterRules. */
  statuses: SalesRepRuleType[];
}

defineProps<IProps>();

const modelValue = defineModel<SalesRepTaskFiltersType>({ required: true });

type PriorityItemType = { value: string; label: string };

const { t } = useI18n();
const { options: customerOptions, failed: customersFailed } = useSalesRepCustomerOptions();

const priorityItems = computed<PriorityItemType[]>(() =>
  TASK_PRIORITIES.map((value) => ({ value, label: t(taskPriorityLabelKey(value)) })),
);

const selectedCustomers = computed(() =>
  customerOptions.value.filter((option) => modelValue.value.customerIds.includes(option.organizationId)),
);

const selectedPriorities = computed(() =>
  priorityItems.value.filter((item) => modelValue.value.priorities.includes(item.value)),
);

function changeCustomers(items: SalesRepCustomerOptionType[]): void {
  modelValue.value = { ...modelValue.value, customerIds: items.map((item) => item.organizationId) };
}

function changeStatus(status?: string): void {
  modelValue.value = { ...modelValue.value, status: status || undefined };
}

function changePriorities(items: PriorityItemType[]): void {
  modelValue.value = { ...modelValue.value, priorities: items.map((item) => item.value) };
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-task-filters {
  @apply flex flex-col gap-3 sm:flex-row;

  // The multi-selects read "N items selected" next to their clear and chevron buttons, so they take more room.
  &__select {
    @apply sm:w-44 sm:shrink-0;

    &--multiple {
      @apply sm:w-52;
    }
  }
}
</style>
