<template>
  <LayoutWidget :title="t('sales_rep.activity.my_activity.title')" size="md" class="my-activity">
    <template #append>
      <VcLink :to="{ name: ACTIVITIES_ROUTE_NAME }" class="my-activity__all-link">
        {{ t("sales_rep.activity.view_all") }}

        <VcIcon name="arrow-right" size="xs" />
      </VcLink>
    </template>

    <template #default-container>
      <div class="my-activity__body">
        <!-- A failure replaces the list: apollo keeps stale rows on a failed refetch (VCST-5586). -->
        <VcEmptyView v-if="failed && !loading" :text="t('sales_rep.activity.load_failed')" variant="error" />

        <!-- A mixed feed: empty is not a quiet year when analytics did not answer — the flag says which. -->
        <VcEmptyView v-else-if="!items.length && !loading" :text="emptyText" icon="activity" />

        <!-- Same first-load skeleton as the insights widgets — the GA-backed query can run for seconds. -->
        <div v-else-if="loading && !items.length" class="my-activity__skeletons" aria-hidden="true">
          <div
            v-for="index in skeletonRows"
            :key="index"
            class="my-activity__skeleton"
            data-test-id="my-activity-skeleton"
          />
        </div>

        <template v-else>
          <div class="my-activity__list">
            <ActivityRow v-for="item in items" :key="activityKey(item)" :item="item" compact show-organization />
          </div>

          <!-- Orders and customers still list when analytics did not answer, so say what is missing from them. -->
          <p v-if="isTrackingMissing" class="my-activity__notice" data-test-id="my-activity-incomplete-label">
            <VcIcon class="my-activity__notice-icon" name="circle-alert" :size="15" />
            {{ t("sales_rep.activity.incomplete") }}
          </p>
        </template>
      </div>
    </template>
  </LayoutWidget>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useSalesRepActivities } from "../composables/useSalesRepActivities";
import { useSharedSalesRepCustomersCount } from "../composables/useSalesRepCustomersCount";
import { useSalesRepPeriodFilter } from "../composables/useSalesRepPeriodFilter";
import { isSalesRepAnalyticsEnabled } from "../composables/useSalesRepsConfig";
import { ACTIVITIES_ROUTE_NAME, HUB_ACTIVITY_PERIOD, MY_ACTIVITY_TAKE } from "../constants";
import { activityKey } from "../utils";
import ActivityRow from "./activity-row.vue";
import LayoutWidget from "./layout-widget.vue";

const { t } = useI18n();

// Latest events across ALL assigned accounts, this year only (the window the Activities page opens on).
// No badges, so no counts are selected: the widget waits only for its own rows.
const { from: periodFrom, to: periodTo } = useSalesRepPeriodFilter(HUB_ACTIVITY_PERIOD);
const { items, loading, error, analyticsUnavailable } = useSalesRepActivities({
  take: MY_ACTIVITY_TAKE,
  periodFrom,
  periodTo,
  withCategoryCounts: false,
});

const { hasNoCustomers } = useSharedSalesRepCustomersCount();

// Without the analytics module the card is an orders-and-customers feed and says nothing about tracking.
const isAnalyticsEnabled = isSalesRepAnalyticsEnabled();
const isTrackingMissing = computed(() => isAnalyticsEnabled && analyticsUnavailable.value);

const emptyText = computed(() => {
  // "No activity this year" read as quiet customers when there are none.
  if (hasNoCustomers.value) {
    return t("sales_rep.activity.no_customers");
  }
  return isTrackingMissing.value
    ? t("sales_rep.customer_insights.analytics_unavailable")
    : t("sales_rep.activity.my_activity.empty");
});

// As many skeleton rows as the widget will show, so the height holds when data arrives.
const skeletonRows = MY_ACTIVITY_TAKE;

const failed = computed(() => Boolean(error.value));
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.my-activity {
  // min-height ≈ five compact rows, so the card keeps its footprint from skeleton to rows or empty.
  &__body {
    @apply min-h-60 px-6 pb-4 pt-1;
  }

  &__list {
    @apply flex flex-col divide-y divide-neutral-100;
  }

  &__notice {
    @apply flex items-start gap-2 border-t border-neutral-100 pt-3 text-sm text-neutral-600;
  }

  &__notice-icon {
    @apply mt-0.5 shrink-0 text-warning;
  }

  &__skeletons {
    @apply flex flex-col;
  }

  &__skeleton {
    @apply my-3 h-12 rounded-[--vc-radius] bg-neutral-100 motion-safe:animate-pulse;
  }

  &__all-link {
    @apply inline-flex items-center gap-1 whitespace-nowrap text-sm text-[--link-color] hover:text-[--link-hover-color];
  }
}
</style>
