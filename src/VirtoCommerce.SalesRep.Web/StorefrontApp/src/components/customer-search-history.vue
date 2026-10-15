<template>
  <!-- Chrome-less: the Customer activity widget's "Searches" sub-view owns the box and the title. -->
  <div class="customer-search-history">
    <div class="customer-search-history__filter">
      <SalesRepRuleChips
        v-model="sortChip"
        :rules="sortChipRules"
        :all-label="t('sales_rep.customer_insights.recent')"
      />

      <!-- The window, named beside the rows: the empty text names it only when there are none. -->
      <span class="customer-search-history__period" data-test-id="customer-search-history-period-label">
        {{ t("sales_rep.activity.period.year") }}
      </span>
    </div>

    <div class="customer-search-history__content">
      <!-- A failure replaces stale rows — same state ladder as top-sellers.vue (VCST-5586). -->
      <VcEmptyView
        v-if="failed && !loading"
        :text="t('sales_rep.customer_insights.search_history.load_failed')"
        variant="error"
      />

      <VcEmptyView
        v-else-if="unavailable && !loading"
        :text="t('sales_rep.customer_insights.analytics_unavailable')"
        icon="search"
      />

      <VcEmptyView
        v-else-if="!items.length && !loading"
        :text="t('sales_rep.customer_insights.search_history.empty_this_year')"
        icon="search"
      />

      <template v-else>
        <ul class="customer-search-history__list">
          <!-- On every request, not only the first: no-cache keeps the previous rows until the answer, so a switch
               between Recent and Top would leave the other mode's list under the new chip. -->
          <template v-if="loading">
            <li
              v-for="index in INSIGHTS_DEFAULT_ROWS"
              :key="index"
              class="customer-search-history__skeleton"
              data-test-id="customer-search-history-skeleton"
              aria-hidden="true"
            />
          </template>

          <template v-else>
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
          </template>
        </ul>

        <p
          v-if="items.length"
          class="customer-search-history__caveat"
          data-test-id="customer-search-history-caveat-label"
        >
          {{ t("sales_rep.customer_insights.tracked_caveat") }}
        </p>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useSalesRepPeriodFilter } from "../composables/useSalesRepPeriodFilter";
import { useSalesRepSearchHistory } from "../composables/useSalesRepSearchHistory";
import {
  HUB_ACTIVITY_PERIOD,
  INSIGHTS_DEFAULT_ROWS,
  INSIGHTS_SORT_BY_COUNT,
  INSIGHTS_SORT_BY_DATE,
} from "../constants";
import { formatStatCount, searchResultsRoute } from "../utils";
import SalesRepRuleChips from "./sales-rep-rule-chips.vue";
import type { SalesRepRuleType } from "../types";

interface IProps {
  organizationId: string;
}

const props = defineProps<IProps>();

const { t, d } = useI18n();

// Recent is the baseline: the newest searches are what the customer is asking about now; Top is the second look.
const sortChip = ref<string | undefined>(undefined);
const sortChipRules = computed<SalesRepRuleType[]>(() => [
  { name: INSIGHTS_SORT_BY_COUNT, label: t("sales_rep.customer_insights.top") },
]);
const sort = computed(() => sortChip.value ?? INSIGHTS_SORT_BY_DATE);

// This year, like the hub's other activity surfaces; the label and the empty text above name it.
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

  &__skeleton {
    @apply my-3 h-9 rounded-[--vc-radius] bg-neutral-100 motion-safe:animate-pulse;
  }

  &__caveat {
    @apply border-t border-neutral-100 pt-3 text-xs text-neutral-600;
  }
}
</style>
