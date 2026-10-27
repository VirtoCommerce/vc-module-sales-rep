<template>
  <!-- The Customer activity widget's "Searches" sub-view. -->
  <CustomerInsightsPanel
    v-model:sort="sort"
    class="customer-search-history"
    :loading="loading"
    :failed="failed"
    :unavailable="unavailable"
    :has-rows="items.length > 0"
    icon="search"
    :failed-text="t('sales_rep.customer_insights.search_history.load_failed')"
    :empty-text="t('sales_rep.customer_insights.search_history.empty_this_year')"
  >
    <li
      v-for="item in items"
      :key="item.term"
      class="customer-search-history__row"
      data-test-id="customer-search-history-row"
    >
      <span class="customer-search-history__term-line">
        <VcLink
          class="customer-search-history__term"
          :to="searchResultsRoute(item.term)"
          target="_blank"
          rel="noopener noreferrer"
        >
          {{ item.term }}
        </VcLink>

        <span class="customer-search-history__count" data-test-id="customer-search-history-count-label">
          {{ searchCountLabel(item.count) }}
        </span>
      </span>

      <span v-if="item.lastSearchedDate" class="customer-search-history__date">
        {{ lastSearchedLabel(item.lastSearchedDate) }}
      </span>
    </li>
  </CustomerInsightsPanel>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useSalesRepPeriodFilter } from "../composables/useSalesRepPeriodFilter";
import { useSalesRepSearchHistory } from "../composables/useSalesRepSearchHistory";
import { HUB_ACTIVITY_PERIOD, INSIGHTS_DEFAULT_ROWS, INSIGHTS_SORT_BY_DATE } from "../constants";
import { formatStatCount, searchResultsRoute } from "../utils";
import CustomerInsightsPanel from "./customer-insights-panel.vue";

interface IProps {
  organizationId: string;
}

const props = defineProps<IProps>();

const { t, d } = useI18n();

// Recent is the baseline: the newest searches are what the customer is asking about now; Top is the second look.
const sort = ref<string>(INSIGHTS_SORT_BY_DATE);

// This year, like the hub's other activity surfaces; the label and the empty text name it.
const { from: periodFrom, to: periodTo } = useSalesRepPeriodFilter(HUB_ACTIVITY_PERIOD);

const { items, unavailable, loading, error } = useSalesRepSearchHistory({
  organizationId: () => props.organizationId,
  sort: () => sort.value,
  periodFrom,
  periodTo,
  take: INSIGHTS_DEFAULT_ROWS,
});

const failed = computed(() => Boolean(error.value));

function searchCountLabel(count: number): string {
  return t("sales_rep.customer_insights.search_history.count", { n: formatStatCount(count) }, count);
}

function lastSearchedLabel(date: string): string {
  return t("sales_rep.customer_insights.search_history.last_searched", { date: d(new Date(date), "short") });
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-search-history {
  &__row {
    @apply flex flex-col gap-0.5 border-b border-neutral-100 py-3 last:border-b-0;
  }

  &__term-line {
    @apply flex items-baseline justify-between gap-4;
  }

  &__term {
    @apply min-w-0 text-sm text-[--link-color] [word-break:break-word] hover:underline;
  }

  &__count {
    @apply flex-none text-sm text-neutral-500;
  }

  &__date {
    @apply text-xs text-neutral-600;
  }
}
</style>
