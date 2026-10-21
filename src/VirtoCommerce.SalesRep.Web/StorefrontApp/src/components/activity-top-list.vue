<template>
  <!-- Ranked counts, not events: no timestamps, one capped list, no pager. -->
  <ol class="activity-top-list">
    <li v-for="(row, index) in rows" :key="row.key" class="activity-top-list__row" data-test-id="activity-top-list-row">
      <span class="activity-top-list__rank" data-test-id="activity-top-list-rank-label">{{ index + 1 }}</span>

      <!-- Only a row with somewhere to go links; an unresolved product code stays plain text. -->
      <VcLink
        v-if="row.to"
        class="activity-top-list__link"
        :to="row.to"
        :title="row.label"
        target="_blank"
        rel="noopener noreferrer"
      >
        {{ row.label }}
      </VcLink>

      <span v-else class="activity-top-list__name" :title="row.label">{{ row.label }}</span>

      <span class="activity-top-list__count">{{ row.count }}</span>
    </li>
  </ol>
</template>

<script setup lang="ts">
import type { SalesRepRankedRowType } from "../types/insights";

interface IProps {
  rows: SalesRepRankedRowType[];
}

defineProps<IProps>();
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.activity-top-list {
  @apply flex flex-col divide-y divide-neutral-100 px-6 py-2;

  &__row {
    @apply flex items-baseline gap-3 py-3;
  }

  &__rank {
    @apply w-5 flex-none text-end text-sm font-bold text-neutral-600;
  }

  // Two lines at most; the full label stays in the title.
  &__link {
    @apply line-clamp-2 min-w-0 text-sm text-[--link-color] [word-break:break-word] hover:underline;
  }

  &__name {
    @apply line-clamp-2 min-w-0 text-sm [word-break:break-word];
  }

  &__count {
    @apply ms-auto flex-none text-sm font-bold text-neutral-500;
  }
}
</style>
