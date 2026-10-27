<template>
  <!-- Chrome-less: the Customer activity widget's sub-view owns the box and the title. The Searches and Product views
       lists differ only in their rows, which come through the default slot. -->
  <div class="customer-insights-panel">
    <div class="customer-insights-panel__filter">
      <SalesRepRuleChips
        v-model="sortChip"
        :rules="sortChipRules"
        :all-label="t('sales_rep.customer_insights.recent')"
      />

      <!-- The window, named beside the rows: the empty text names it only when there are none. -->
      <span class="customer-insights-panel__period" data-test-id="customer-insights-panel-period-label">
        {{ t("sales_rep.activity.period.year") }}
      </span>
    </div>

    <div class="customer-insights-panel__content">
      <!-- A failure replaces stale rows — same state ladder as top-sellers.vue (VCST-5586). -->
      <VcEmptyView v-if="failed && !loading" :text="failedText" variant="error" />

      <VcEmptyView
        v-else-if="unavailable && !loading"
        :text="t('sales_rep.customer_insights.analytics_unavailable')"
        :icon="icon"
      />

      <VcEmptyView v-else-if="!hasRows && !loading" :text="emptyText" :icon="icon" />

      <template v-else>
        <ul class="customer-insights-panel__list">
          <!-- On every request, not only the first: no-cache keeps the previous rows until the answer, so a switch
               between Recent and Top would leave the other mode's list under the new chip. -->
          <template v-if="loading">
            <li
              v-for="index in INSIGHTS_DEFAULT_ROWS"
              :key="index"
              :class="['customer-insights-panel__skeleton', { 'customer-insights-panel__skeleton--tall': tallRows }]"
              data-test-id="customer-insights-panel-skeleton"
              aria-hidden="true"
            />
          </template>

          <slot v-else />
        </ul>

        <p v-if="hasRows" class="customer-insights-panel__caveat" data-test-id="customer-insights-panel-caveat-label">
          {{ t("sales_rep.customer_insights.tracked_caveat") }}
        </p>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { INSIGHTS_DEFAULT_ROWS, INSIGHTS_SORT_BY_COUNT, INSIGHTS_SORT_BY_DATE } from "../constants";
import SalesRepRuleChips from "./sales-rep-rule-chips.vue";
import type { SalesRepRuleType } from "../types";

interface IProps {
  loading: boolean;
  failed: boolean;
  unavailable: boolean;
  hasRows: boolean;
  // The list's mark, on its empty and unavailable views.
  icon: string;
  failedText: string;
  emptyText: string;
  // Rows with a thumbnail stand taller, and so do the skeletons that hold their place.
  tallRows?: boolean;
}

defineProps<IProps>();

// "date" or "count": Recent is the baseline chip, and the one rule flips to Top.
const sort = defineModel<string>("sort", { required: true });

const { t } = useI18n();

const sortChip = computed<string | undefined>({
  get: () => (sort.value === INSIGHTS_SORT_BY_COUNT ? INSIGHTS_SORT_BY_COUNT : undefined),
  set: (value) => {
    sort.value = value ?? INSIGHTS_SORT_BY_DATE;
  },
});

const sortChipRules = computed<SalesRepRuleType[]>(() => [
  { name: INSIGHTS_SORT_BY_COUNT, label: t("sales_rep.customer_insights.top") },
]);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-insights-panel {
  @apply flex flex-col;

  // px-6 aligns the chips with the widget header title.
  &__filter {
    @apply flex items-center justify-between gap-3 border-b border-neutral-200 bg-neutral-50 px-6 py-3;
  }

  &__period {
    @apply flex-none text-xs text-neutral-500;
  }

  &__content {
    @apply px-6 pb-4 pt-1;
  }

  &__list {
    @apply flex flex-col;
  }

  &__skeleton {
    @apply my-3 h-9 rounded-[--vc-radius] bg-neutral-100 motion-safe:animate-pulse;

    &--tall {
      @apply h-12;
    }
  }

  &__caveat {
    @apply border-t border-neutral-100 pt-3 text-xs text-neutral-600;
  }
}
</style>
