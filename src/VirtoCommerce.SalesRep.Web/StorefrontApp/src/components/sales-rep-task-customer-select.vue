<template>
  <!-- `|| undefined`: VcSelect counts "" as a selection, and "" is the form's own convention for "none". -->
  <VcSelect
    :model-value="modelValue || undefined"
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
const { options, failed } = useSalesRepCustomerOptions();

// The options stop at a cap, so the linked customer may be past it: list it anyway, or the form would drop it.
const items = computed(() =>
  !props.modelValue || options.value.some((option) => option.organizationId === props.modelValue)
    ? options.value
    : [{ organizationId: props.modelValue, organizationName: props.name || props.modelValue }, ...options.value],
);
</script>
