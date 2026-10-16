<template>
  <VcSelect
    v-model="model"
    class="time-select"
    :items="items"
    text-field="label"
    value-field="value"
    :label="label"
    :disabled="disabled"
    :required="required"
    enable-teleport
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { formatTimeOfDay, timeKeyToDate, timeKeysOfDay } from "../tasks";

interface IProps {
  label?: string;
  disabled?: boolean;
  required?: boolean;
  stepMinutes: number;
}

const props = defineProps<IProps>();

/** "HH:mm" on the user's clock. */
const model = defineModel<string>({ required: true });

const { locale } = useI18n();

// A time off the grid (set in the admin task UI, say) is listed too, so opening a form never moves it.
const items = computed(() => {
  const keys = timeKeysOfDay(props.stepMinutes);
  if (model.value && !keys.includes(model.value)) {
    keys.push(model.value);
    keys.sort();
  }

  // The formatter a call's line in the task list uses too, so both read alike.
  return keys.map((value) => ({ value, label: formatTimeOfDay(timeKeyToDate(value), locale.value) }));
});
</script>
