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
import { TIME_OF_DAY_FORMAT, timeKeyToDate, timeKeysOfDay } from "../tasks";

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

  // One formatter for the list: vue-i18n's `d` builds a new one per call when given options without a format key.
  const format = new Intl.DateTimeFormat(locale.value, TIME_OF_DAY_FORMAT);

  return keys.map((value) => ({ value, label: format.format(timeKeyToDate(value)) }));
});
</script>
