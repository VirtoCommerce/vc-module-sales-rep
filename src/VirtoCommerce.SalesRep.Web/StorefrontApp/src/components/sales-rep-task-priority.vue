<template>
  <span class="sales-rep-task-priority">
    <!-- Decoration: the label says the same. -->
    <span :class="`sales-rep-task-priority__dot sales-rep-task-priority__dot--${level}`" aria-hidden="true" />

    {{ t(taskPriorityLabelKey(priority)) }}
  </span>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { taskPriorityLabelKey } from "../tasks";

interface IProps {
  /** A TaskPriority name. */
  priority: string;
}

const props = defineProps<IProps>();

const { t } = useI18n();

// The mockup has three colours for the five priorities: the top two red, the middle one orange, the bottom two grey.
const LEVELS: Record<string, "high" | "normal" | "low"> = {
  highest: "high",
  high: "high",
  normal: "normal",
};

const level = computed(() => LEVELS[props.priority.toLowerCase()] ?? "low");
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-task-priority {
  @apply inline-flex items-center gap-1.5 text-sm;

  &__dot {
    @apply size-2 shrink-0 rounded-full;

    &--high {
      background-color: var(--color-danger-500);
    }

    &--normal {
      background-color: var(--color-warning-500);
    }

    &--low {
      background-color: var(--color-neutral-400);
    }
  }
}
</style>
