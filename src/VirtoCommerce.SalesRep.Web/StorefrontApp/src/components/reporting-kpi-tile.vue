<template>
  <div class="reporting-kpi-tile" :data-test-id="testId">
    <span class="reporting-kpi-tile__label">{{ label }}</span>

    <strong class="reporting-kpi-tile__value">{{ value }}</strong>

    <VcBadge
      v-if="change !== null && change !== undefined"
      class="reporting-kpi-tile__change"
      :color="change < 0 ? 'danger' : 'success'"
      variant="soft"
      size="sm"
      rounded
      nowrap
    >
      {{ t("sales_rep.reporting.kpi.vs_last_year", { change: formatChange(change) }) }}
    </VcBadge>

    <span v-if="caption" class="reporting-kpi-tile__caption">{{ caption }}</span>

    <span
      v-if="progress !== undefined"
      class="reporting-kpi-tile__track"
      role="progressbar"
      :aria-valuenow="Math.round(clampedProgress * 100)"
      aria-valuemin="0"
      aria-valuemax="100"
      :aria-label="label"
    >
      <span class="reporting-kpi-tile__fill" :style="{ width: `${clampedProgress * 100}%` }" />
    </span>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

interface IProps {
  label: string;
  value: string;
  /** Percent vs last year; the badge is hidden without one. */
  change?: number | null;
  caption?: string;
  /** 0–1; draws the bar under the caption. */
  progress?: number;
  testId?: string;
}

const props = defineProps<IProps>();

const { t, n } = useI18n();

const clampedProgress = computed(() => Math.min(1, Math.max(0, props.progress ?? 0)));

function formatChange(value: number): string {
  const percent = n(Math.abs(value) / 100, { style: "percent", minimumFractionDigits: 1, maximumFractionDigits: 1 });
  return `${value < 0 ? "↓" : "↑"} ${percent}`;
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.reporting-kpi-tile {
  @apply flex min-w-0 flex-col items-start gap-1 rounded-[--vc-radius] border border-neutral-200 p-3;

  &__label {
    @apply text-xs font-bold text-neutral-600;
  }

  &__value {
    @apply text-2xl font-bold text-neutral-950;
  }

  &__caption {
    @apply text-xs text-neutral-600;
  }

  &__track {
    @apply mt-1 block h-1 w-full overflow-hidden rounded-full bg-neutral-200;
  }

  &__fill {
    @apply block h-full rounded-full bg-primary-500;
  }
}
</style>
