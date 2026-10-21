<template>
  <!-- The Customer activity widget's "Product views" sub-view. -->
  <CustomerInsightsPanel
    v-model:sort="sort"
    class="customer-browse-history"
    :loading="loading"
    :failed="failed"
    :unavailable="unavailable"
    :has-rows="items.length > 0"
    icon="eye"
    :failed-text="t('sales_rep.customer_insights.browse_history.load_failed')"
    :empty-text="t('sales_rep.customer_insights.browse_history.empty_this_year')"
    tall-rows
  >
    <li
      v-for="item in items"
      :key="item.sku"
      class="customer-browse-history__row"
      data-test-id="customer-browse-history-row"
    >
      <span class="customer-browse-history__thumb">
        <!-- The name beside it already says what this is. -->
        <VcImage v-if="item.imageUrl" :src="item.imageUrl" alt="" class="customer-browse-history__thumb-img" />

        <VcIcon v-else name="cube" aria-hidden="true" />
      </span>

      <span class="customer-browse-history__text">
        <!-- Only a row resolved to a real product links; an unresolved code stays plain text. -->
        <VcLink
          v-if="item.productId"
          :to="getProductRoute(item.productId)"
          :title="item.name || item.sku"
          target="_blank"
          rel="noopener noreferrer"
          class="customer-browse-history__name"
        >
          {{ item.name || item.sku }}
        </VcLink>

        <span v-else class="customer-browse-history__name" :title="item.name || item.sku">
          {{ item.name || item.sku }}
        </span>

        <span
          v-if="item.sku && item.name"
          class="customer-browse-history__sku"
          data-test-id="customer-browse-history-sku-label"
        >
          {{ item.sku }}
        </span>

        <span class="customer-browse-history__meta" data-test-id="customer-browse-history-meta-label">
          {{ rowMeta(item) }}
        </span>
      </span>
    </li>
  </CustomerInsightsPanel>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { formatStatCount, getProductRoute } from "@vc-frontend/core";
import { useSalesRepBrowseHistory } from "../composables/useSalesRepBrowseHistory";
import { useSalesRepPeriodFilter } from "../composables/useSalesRepPeriodFilter";
import { HUB_ACTIVITY_PERIOD, INSIGHTS_DEFAULT_ROWS, INSIGHTS_SORT_BY_DATE } from "../constants";
import CustomerInsightsPanel from "./customer-insights-panel.vue";
import type { SalesRepBrowsedProductRowType } from "../types/insights";

interface IProps {
  organizationId: string;
}

const props = defineProps<IProps>();

const { t, d } = useI18n();

// Recent is the baseline (the ticket asks for recent product views); the one rule flips to most viewed.
const sort = ref<string>(INSIGHTS_SORT_BY_DATE);

// This year, like the hub's other activity surfaces; the label and the empty text name it.
const { from: periodFrom, to: periodTo } = useSalesRepPeriodFilter(HUB_ACTIVITY_PERIOD);

const { items, unavailable, loading, error } = useSalesRepBrowseHistory({
  organizationId: () => props.organizationId,
  sort: () => sort.value,
  periodFrom,
  periodTo,
  take: INSIGHTS_DEFAULT_ROWS,
});

const failed = computed(() => Boolean(error.value));

function rowMeta(item: SalesRepBrowsedProductRowType): string {
  const parts = [
    t("sales_rep.customer_insights.browse_history.views", { n: formatStatCount(item.viewCount) }, item.viewCount),
  ];
  if (item.lastViewedDate) {
    parts.push(d(new Date(item.lastViewedDate), "short"));
  }
  return parts.join(" · ");
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-browse-history {
  &__row {
    @apply flex items-center gap-3 border-b border-neutral-100 py-3 last:border-b-0;
  }

  &__thumb {
    --vc-icon-size: 1.25rem;

    @apply flex size-10 flex-none items-center justify-center overflow-hidden rounded-[--vc-radius] border border-neutral-200 bg-additional-50 text-neutral-400;
  }

  &__thumb-img {
    @apply size-full object-contain;
  }

  &__text {
    @apply flex min-w-0 flex-col;
  }

  // Two lines at most; the full name stays in the title.
  &__name {
    @apply line-clamp-2 text-sm [word-break:break-word];
  }

  &__sku {
    @apply text-xs text-neutral-500;
  }

  &__meta {
    @apply text-xs text-neutral-600;
  }
}
</style>
