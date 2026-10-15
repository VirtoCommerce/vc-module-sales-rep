<template>
  <VcSelect
    :model-value="modelValue"
    class="time-select"
    :items="items"
    text-field="label"
    value-field="value"
    :label="label"
    :disabled="disabled"
    :required="required"
    enable-teleport
    @update:model-value="emit('update:modelValue', $event as string)"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { timeKeyToDate, timeKeysOfDay } from "../tasks";

interface IProps {
  /** "HH:mm" on the user's clock. */
  modelValue: string;
  label?: string;
  disabled?: boolean;
  required?: boolean;
  stepMinutes?: number;
}

const props = withDefaults(defineProps<IProps>(), { stepMinutes: 15 });

const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const { d } = useI18n();

// A time off the grid (set in the admin task UI, say) is listed too, so opening a form never moves it.
const items = computed(() => {
  const keys = timeKeysOfDay(props.stepMinutes);
  if (props.modelValue && !keys.includes(props.modelValue)) {
    keys.push(props.modelValue);
    keys.sort();
  }

  return keys.map((value) => ({ value, label: d(timeKeyToDate(value), { hour: "numeric", minute: "2-digit" }) }));
});
</script>
