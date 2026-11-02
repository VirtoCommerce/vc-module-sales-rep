<template>
  <!-- `clearable` is bound: VcSelect counts "", the form's "none", as a selection and would offer to clear it. -->
  <VcSelect
    :model-value="modelValue"
    class="sales-rep-task-customer-select"
    :items="items"
    text-field="organizationName"
    value-field="organizationId"
    :label="t('sales_rep.tasks.form.customer_label')"
    :placeholder="t('sales_rep.tasks.form.customer_placeholder')"
    :disabled="disabled"
    :error="failed"
    :message="failed ? t('sales_rep.tasks.form.customer_error') : undefined"
    :clearable="!!modelValue"
    autocomplete
    lazy
    enable-teleport
    @update:model-value="emit('update:modelValue', ($event as string | undefined) ?? '')"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useSalesRepCustomerOptions } from "../composables/useSalesRepCustomerOptions";
import type { SalesRepCustomerOptionType } from "../composables/useSalesRepCustomerOptions";

interface IProps {
  /** Organization id; "" for none. */
  modelValue: string;
  /** The linked customer's name, for when the options do not list it. */
  name?: string;
  disabled?: boolean;
}

const props = defineProps<IProps>();

const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const { t } = useI18n();
const { options, failed, findOption } = useSalesRepCustomerOptions();

// The options stop at a cap, so the linked customer may be past it: list it anyway, or the form would drop it.
// Sorted in by name like the sharing picker's, rather than put above every other customer.
const items = computed(() => {
  if (!props.modelValue || findOption(props.modelValue)) {
    return options.value;
  }

  const linked: SalesRepCustomerOptionType = {
    organizationId: props.modelValue,
    organizationName: props.name || props.modelValue,
    location: "",
    imageUrl: "",
  };

  return [linked, ...options.value].sort((a, b) => a.organizationName.localeCompare(b.organizationName));
});
</script>
