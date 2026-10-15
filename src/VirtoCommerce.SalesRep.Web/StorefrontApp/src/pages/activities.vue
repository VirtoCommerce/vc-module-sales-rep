<template>
  <div class="activities">
    <VcBreadcrumbs :items="breadcrumbs" />

    <VcEmptyView
      v-if="customerUnavailable"
      :text="unavailableText"
      :variant="customerFailed ? 'error' : 'empty'"
      icon="outline-404"
    >
      <template #button>
        <VcButton :to="{ name: MY_CUSTOMERS_ROUTE_NAME }" prepend-icon="arrow-left">
          {{ t("sales_rep.customer_profile.back_to_customers") }}
        </VcButton>
      </template>
    </VcEmptyView>

    <template v-else>
      <!-- ?organizationId= narrows the feed to one customer, and the heading names them (as customer-orders does). -->
      <VcTypography class="activities__title" tag="h1">
        {{ heading }}
      </VcTypography>

      <div class="activities__results">
        <div class="activities__controls">
          <SalesRepRuleChips v-model="category" :rules="categoryRules" :all-label="allTabLabel">
            <!-- No name = the All tab, which merges tracked rows in and so carries the mark too. -->
            <template #suffix="{ ruleName }">
              <TrackedMetricHint v-if="isTracked(ruleName)" />
            </template>
          </SalesRepRuleChips>

          <!-- A progression, so All time comes last. -->
          <div class="activities__period">
            <SalesRepRuleChip
              v-for="option in PERIODS"
              :key="option"
              :value="option"
              :model-value="period"
              :label="t(PERIOD_LABEL_KEYS[option])"
              @change="period = $event"
            />
          </div>
        </div>

        <!-- Top|Recent, only on the tracked categories that rank (searches, product views); Recent is the baseline. -->
        <div v-if="isRankableTab" class="activities__mode">
          <SalesRepRuleChips
            v-model="modeChip"
            :rules="modeRules"
            :all-label="t('sales_rep.customer_insights.recent')"
          />
        </div>

        <!-- The badges and the rows are separate reads, and either can come back without analytics while the other
             shows tracked figures: say so, rather than leave a "–" beside listed rows to explain itself. Mounted with
             the page and only its text swapped: a live region that appears together with its text is not reliably
             announced. -->
        <p
          class="activities__notice"
          role="status"
          aria-live="polite"
          aria-atomic="true"
          data-test-id="activities-incomplete-label"
        >
          <template v-if="showIncompleteNotice">
            <VcIcon class="activities__notice-icon" name="circle-alert" :size="15" aria-hidden="true" />
            {{ t("sales_rep.activity.incomplete") }}
          </template>
        </p>

        <!-- A failure gets its own view — it must not read as "no activity" (VCST-5586). -->
        <VcEmptyView v-if="viewFailed && !viewLoading" :text="failedText" variant="error" />

        <VcEmptyView v-else-if="viewEmpty && !viewLoading" :text="emptyText" :icon="emptyIcon" />

        <VcWidget v-else size="md">
          <template #default-container>
            <!-- Skeleton on every fetch, as VcTable's loading prop does: the GA-backed query can run for seconds. -->
            <div v-if="viewLoading" class="activities__skeletons" aria-hidden="true">
              <div
                v-for="index in skeletonRows"
                :key="index"
                class="activities__skeleton"
                data-test-id="activities-skeleton"
              />
            </div>

            <ActivityTopList v-else-if="topMode" :rows="topRows" />

            <div v-else class="activities__list">
              <ActivityRow
                v-for="item in items"
                :key="activityKey(item)"
                :item="item"
                :show-organization="!organizationId"
              />
            </div>
          </template>
        </VcWidget>

        <!-- Outside the list's states, as VcTable keeps its own: a page that is loading keeps the pager, and the focus
             on the button just pressed, and a page that came back empty still has a way to the others. -->
        <VcPagination
          v-if="showPager"
          v-model:page="page"
          :pages="pages"
          :compact="isPhone"
          class="activities__pagination"
          @update:page="scrollToTop"
        />

        <p v-if="showCaveat && !viewFailed" class="activities__caveat" data-test-id="activities-caveat-label">
          <VcIcon name="info" :size="14" aria-hidden="true" />
          {{ t("sales_rep.activity.caveat") }}
        </p>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { useBreakpoints } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { BREAKPOINTS, getProductRoute, ROUTES, useBreadcrumbs, usePageHead } from "@vc-frontend/core";
import ActivityRow from "../components/activity-row.vue";
import ActivityTopList from "../components/activity-top-list.vue";
import SalesRepRuleChip from "../components/sales-rep-rule-chip.vue";
import SalesRepRuleChips from "../components/sales-rep-rule-chips.vue";
import TrackedMetricHint from "../components/tracked-metric-hint.vue";
import { useSalesRepActivities } from "../composables/useSalesRepActivities";
import { useSalesRepBrowseHistory } from "../composables/useSalesRepBrowseHistory";
import { useSalesRepCustomer } from "../composables/useSalesRepCustomer";
import { useSharedSalesRepCustomersCount } from "../composables/useSalesRepCustomersCount";
import { useSalesRepPeriodFilter } from "../composables/useSalesRepPeriodFilter";
import { isSalesRepAnalyticsEnabled } from "../composables/useSalesRepsConfig";
import { useSalesRepSearchHistory } from "../composables/useSalesRepSearchHistory";
import {
  ACTIVITY_CATEGORIES,
  ACTIVITY_CATEGORY,
  ACTIVITY_MAX_SKIP,
  ACTIVITY_PAGE_SIZE,
  CUSTOMER_PROFILE_ROUTE_NAME,
  DASHBOARD_ROUTE_NAME,
  HUB_ACTIVITY_PERIOD,
  INSIGHTS_MAX_ROWS,
  INSIGHTS_SORT_BY_COUNT,
  RANKED_ACTIVITY_CATEGORIES,
  TRACKED_ACTIVITY_CATEGORIES,
  MY_CUSTOMERS_ROUTE_NAME,
} from "../constants";
import { activityCategoryIcon, activityKey, formatStatCount, searchResultsRoute } from "../utils";
import type { ActivityCategoryType } from "../constants";
import type { SalesRepPeriodType } from "../composables/useSalesRepPeriodFilter";
import type { SalesRepRuleType } from "../types";
import type { SalesRepRankedRowType } from "../types/insights";

interface IProps {
  // Optional narrowing to one customer (from the customer-activity widget's "All activity" link).
  organizationId?: string;
}

const props = defineProps<IProps>();

const CATEGORY_TAB_KEYS: Record<ActivityCategoryType, string> = {
  orders: "sales_rep.activity.tabs.orders",
  customers: "sales_rep.activity.tabs.customers",
  searches: "sales_rep.activity.tabs.searches",
  productViews: "sales_rep.activity.tabs.product_views",
  logins: "sales_rep.activity.tabs.logins",
};

const PERIODS: readonly SalesRepPeriodType[] = ["month", "year", "lifetime"];

const PERIOD_LABEL_KEYS: Record<SalesRepPeriodType, string> = {
  month: "sales_rep.activity.period.month",
  year: "sales_rep.activity.period.year",
  lifetime: "sales_rep.activity.period.all_time",
};

// Recent (the feed) is the baseline; the one rule flips to the ranked Top list, as in the customer panels.
const TOP_MODE_RULE = "top";

// A tracked tab shows "–" when analytics did not answer: a "(0)" in the tab row reads as "they searched
// nothing". Orders and Customers keep real counts; a failed counts request dashes every tab.
const UNMEASURED_BADGE = "–";

// Match the rows being replaced so the page height holds during a refetch; a handful on first load.
const FIRST_LOAD_SKELETON_ROWS = 5;

// totalCount counts rows the backend will not serve past its skip cap (see ACTIVITY_MAX_SKIP), so the
// pager stops at the deepest page that still comes back with rows.
const MAX_PAGES = Math.floor(ACTIVITY_MAX_SKIP / ACTIVITY_PAGE_SIZE) + 1;

const { t } = useI18n();

// Without the analytics module the tracked categories are left out altogether — tabs, marks, caveat and notice —
// rather than shown unavailable. The module set does not change while the page is open.
const isAnalyticsEnabled = isSalesRepAnalyticsEnabled();
const visibleCategories = isAnalyticsEnabled
  ? ACTIVITY_CATEGORIES
  : ACTIVITY_CATEGORIES.filter((name) => !TRACKED_ACTIVITY_CATEGORIES.has(name));

// Selected category tab; undefined = the "All" baseline (no categories filter).
const category = ref<string | undefined>(undefined);
const page = ref(1);

// Opens on the widgets' window (HUB_ACTIVITY_PERIOD). All time (no bounds) would read GA from 2015 to render a
// page; it stays one chip away.
const { period, from: periodFrom, to: periodTo } = useSalesRepPeriodFilter(HUB_ACTIVITY_PERIOD);

const isRankableTab = computed(() => Boolean(category.value && RANKED_ACTIVITY_CATEGORIES.has(category.value)));
const isSearchesTab = computed(() => category.value === ACTIVITY_CATEGORY.searches);

const modeChip = ref<string | undefined>(undefined);
const modeRules = computed<SalesRepRuleType[]>(() => [
  { name: TOP_MODE_RULE, label: t("sales_rep.customer_insights.top") },
]);
const topMode = computed(() => isRankableTab.value && modeChip.value === TOP_MODE_RULE);

// flush: "sync" resets the page before the variables watcher runs, so a tab/period change fires one
// request, not two.
watch(
  [category, period],
  () => {
    page.value = 1;
  },
  { flush: "sync" },
);

// Switching tabs resets to Recent — a mode picked for one category must not silently carry over.
watch(category, () => {
  modeChip.value = undefined;
});

// A customer switch reuses this instance (same route, new query), so it starts over like a fresh open — the
// window included: All time carried into the rep's whole feed would read every customer's history from 2015.
watch(
  () => props.organizationId,
  () => {
    category.value = undefined;
    modeChip.value = undefined;
    period.value = HUB_ACTIVITY_PERIOD;
    page.value = 1;
  },
  { flush: "sync" },
);

// Only the selected tab's rows: without categoryCounts the backend reads just that category, so Orders and
// Customers never wait on Google. Paused in Top mode, where the ranked list replaces it.
const { items, totalCount, loading, error, analyticsUnavailable } = useSalesRepActivities({
  organizationId: () => props.organizationId,
  categories: () => (category.value ? [category.value] : undefined),
  periodFrom,
  periodTo,
  take: ACTIVITY_PAGE_SIZE,
  skip: () => (page.value - 1) * ACTIVITY_PAGE_SIZE,
  withCategoryCounts: false,
  enabled: () => !topMode.value,
});

// The badges in their own request: counting every category is the slow half, and it does not change with the
// tab or the page. take: 0 asks for counts only. No previous result is held: across a customer or period
// switch the held figures are another customer's, or another window's.
const {
  categoryCounts,
  loading: countsLoading,
  error: countsError,
  analyticsUnavailable: countsAnalyticsUnavailable,
} = useSalesRepActivities({
  organizationId: () => props.organizationId,
  periodFrom,
  periodTo,
  take: 0,
  keepPreviousResult: false,
});

// The scoped customer's name — resolved only when the query param narrows the feed.
const {
  customer,
  failed: customerFailed,
  notFound: customerNotFound,
} = useSalesRepCustomer(() => props.organizationId ?? "");
const customerName = computed(() => customer.value?.organizationName);

// An id the rep cannot see would otherwise read as their own feed (fallback heading, every badge 0); only this
// query can tell. Both flags are scoped: no id, no read, and notFound answers only for the id being asked.
const customerUnavailable = computed(() => customerFailed.value || customerNotFound.value);

// The feed and the counts answer an unserved id at once, with nothing, so until the customer resolves they count as
// still loading: "(0)" badges and "No activity" must not show for a customer that turns out not to be the rep's.
// The reads still start together, so a served customer's GA read waits for nothing.
const isCustomerPending = computed(
  () => Boolean(props.organizationId) && !customer.value && !customerUnavailable.value,
);

// The badges count events and the counts request's totalCount counts rows (an analytics row is an hour bucket of
// several events), so "All" adds up the tabs it sits beside — the figure it has to agree with.
const allCount = computed(() => visibleCategories.reduce((sum, name) => sum + countOf(name), 0));

// The tabs show no figures until the counts for this customer and window land.
const countsPending = computed(() => isCustomerPending.value || (countsLoading.value && !categoryCounts.value.length));
const countsFailed = computed(() => Boolean(countsError.value));

// Zero-count categories keep their tab: a rep must see that a category exists and is quiet.
const categoryRules = computed<SalesRepRuleType[]>(() =>
  visibleCategories.map((name) => ({ name, label: tabLabel(t(CATEGORY_TAB_KEYS[name]), name) })),
);

const allTabLabel = computed(() => tabLabel(t("sales_rep.activity.tabs.all")));

const failed = computed(() => Boolean(error.value));

// Each Top list runs only while its tab shows it: no speculative GA reads.
const topSearches = useSalesRepSearchHistory({
  organizationId: () => props.organizationId,
  sort: INSIGHTS_SORT_BY_COUNT,
  periodFrom,
  periodTo,
  take: INSIGHTS_MAX_ROWS,
  enabled: computed(() => topMode.value && isSearchesTab.value),
});

const topViews = useSalesRepBrowseHistory({
  organizationId: () => props.organizationId,
  sort: INSIGHTS_SORT_BY_COUNT,
  periodFrom,
  periodTo,
  take: INSIGHTS_MAX_ROWS,
  enabled: computed(() => topMode.value && category.value === ACTIVITY_CATEGORY.productViews),
});

const topRows = computed<SalesRepRankedRowType[]>(() =>
  isSearchesTab.value
    ? topSearches.items.value.map((item) => ({
        key: item.term,
        label: t("sales_rep.activity.quoted_term", { term: item.term }),
        to: searchResultsRoute(item.term),
        count: t("sales_rep.customer_insights.search_history.count", { n: formatStatCount(item.count) }, item.count),
      }))
    : topViews.items.value.map((item) => ({
        key: item.sku,
        label: item.name || item.sku,
        to: item.productId ? getProductRoute(item.productId) : undefined,
        count: t(
          "sales_rep.customer_insights.browse_history.views",
          { n: formatStatCount(item.viewCount) },
          item.viewCount,
        ),
      })),
);

// The state ladder speaks for whichever list the current tab ranks.
const topList = computed(() => (isSearchesTab.value ? topSearches : topViews));
const topLoading = computed(() => topList.value.loading.value);
const topFailed = computed(() => Boolean(topList.value.error.value));
const topUnavailable = computed(() => topList.value.unavailable.value);

const viewLoading = computed(() => isCustomerPending.value || (topMode.value ? topLoading.value : loading.value));
const viewFailed = computed(() => (topMode.value ? topFailed.value : failed.value));
const viewEmpty = computed(() => (topMode.value ? topUnavailable.value || !topRows.value.length : !items.value.length));

// Only beside rows: an empty view already says why it is empty.
const figuresIncomplete = computed(
  () => isTracked() && (countsFailed.value || countsAnalyticsUnavailable.value || analyticsUnavailable.value),
);
const showIncompleteNotice = computed(
  () => figuresIncomplete.value && !viewLoading.value && !viewFailed.value && !viewEmpty.value,
);

const skeletonRows = computed(
  () => (topMode.value ? topRows.value.length : items.value.length) || FIRST_LOAD_SKELETON_ROWS,
);

const failedText = computed(() => {
  if (!topMode.value) {
    return t("sales_rep.activity.load_failed");
  }
  return isSearchesTab.value
    ? t("sales_rep.customer_insights.search_history.load_failed")
    : t("sales_rep.customer_insights.browse_history.load_failed");
});

// The caveat concerns tracked (GA-sourced) rows, so it shows for those tabs and for the mixed "All" view.
const showCaveat = computed(() => isTracked(category.value));

const { hasNoCustomers } = useSharedSalesRepCustomersCount();

// Top mode reuses the customer panels' "No tracked …" wording and their unavailable state.
const emptyText = computed(() => {
  // Nothing to show on any tab or period of a rep's own feed: "no activity in this period" read as quiet customers.
  if (!props.organizationId && hasNoCustomers.value) {
    return t("sales_rep.activity.no_customers");
  }
  if (topMode.value) {
    return topEmptyText();
  }
  // An empty tracked tab is a quiet period or a source that never answered; only the flag tells them apart.
  if (analyticsUnavailable.value && showCaveat.value) {
    return t("sales_rep.customer_insights.analytics_unavailable");
  }
  if (category.value) {
    return t("sales_rep.activity.no_results");
  }
  if (period.value === "lifetime") {
    return t("sales_rep.activity.empty");
  }
  return isTracked() ? t("sales_rep.activity.empty_period") : t("sales_rep.activity.empty_period_untracked");
});

const emptyIcon = computed(() => (topMode.value && category.value ? activityCategoryIcon(category.value) : "activity"));

// Icon-only Prev/Next on a phone: with their labels, two pages need ~350 px, wider than a 320 px screen leaves the
// card, and the page scrolled sideways.
const isPhone = useBreakpoints(BREAKPOINTS).smaller("sm");

const pages = computed(() => Math.min(Math.max(1, Math.ceil(totalCount.value / ACTIVITY_PAGE_SIZE)), MAX_PAGES));

// The feed's pager; the ranked list is one capped page.
const showPager = computed(() => !topMode.value && pages.value > 1);

watch(pages, (total) => {
  if (page.value > total) {
    page.value = total;
  }
});

// The customer profile's own wording for the same two situations, reused rather than restated.
const unavailableText = computed(() =>
  customerFailed.value ? t("sales_rep.customer_profile.load_failed") : t("sales_rep.customer_profile.not_found"),
);

// The bare noun stands in until the customer's name resolves, so the heading never renders half-written.
const heading = computed(() => {
  if (!props.organizationId) {
    return t("sales_rep.activity.page.title");
  }
  return customerName.value
    ? t("sales_rep.activity.page.customer_title", { customer: customerName.value })
    : t("sales_rep.activity.page.title_fallback");
});

usePageHead({ title: heading });

const breadcrumbs = useBreadcrumbs(() => {
  const trail = [
    { title: t("common.links.account"), route: { name: ROUTES.ACCOUNT.NAME } },
    { title: t("sales_rep.hub.title"), route: { name: DASHBOARD_ROUTE_NAME } },
  ];

  // The customer segment waits for the name — a crumb with an empty title is worse than no crumb.
  if (!props.organizationId || !customerName.value) {
    return [...trail, { title: t("sales_rep.activity.breadcrumb") }];
  }

  return [
    ...trail,
    { title: t("sales_rep.my_customers.page.title"), route: { name: MY_CUSTOMERS_ROUTE_NAME } },
    {
      title: customerName.value,
      route: { name: CUSTOMER_PROFILE_ROUTE_NAME, params: { organizationId: props.organizationId } },
    },
    { title: t("sales_rep.activity.breadcrumb") },
  ];
});

// Whether a tab carries tracked (GA-sourced) rows. No name = the All tab, which merges them in.
function isTracked(name?: string): boolean {
  return isAnalyticsEnabled && (!name || TRACKED_ACTIVITY_CATEGORIES.has(name));
}

// Every badge reads the counts request, the selected tab included: one figure, one source.
function countOf(name: string): number {
  return categoryCounts.value.find((entry) => entry.category === name)?.count ?? 0;
}

// No name = the All tab, which adds every tab up: a dashed tab dashes it too, as a partial sum beside "–" tabs
// reads as the whole feed.
function tabLabel(label: string, name?: string): string {
  if (countsPending.value) {
    return label;
  }
  const badge =
    countsFailed.value || (countsAnalyticsUnavailable.value && isTracked(name))
      ? UNMEASURED_BADGE
      : formatStatCount(name ? countOf(name) : allCount.value);
  return `${label} (${badge})`;
}

// "In this period" has no period to point at under All time.
function topEmptyText(): string {
  if (topUnavailable.value) {
    return t("sales_rep.customer_insights.analytics_unavailable");
  }
  const isAllTime = period.value === "lifetime";
  if (isSearchesTab.value) {
    return isAllTime
      ? t("sales_rep.customer_insights.search_history.empty_all_time")
      : t("sales_rep.customer_insights.search_history.empty");
  }
  return isAllTime
    ? t("sales_rep.customer_insights.browse_history.empty_all_time")
    : t("sales_rep.customer_insights.browse_history.empty");
}

function scrollToTop(): void {
  window.scroll({ top: 0, behavior: "smooth" });
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.activities {
  &__title {
    @apply [word-break:break-word];
  }

  &__results {
    @apply mt-4 flex flex-col gap-4;
  }

  &__controls {
    @apply flex flex-wrap items-center justify-between gap-3;
  }

  // The rule-chips row's own layout.
  &__period {
    @apply flex flex-wrap items-center gap-1;
  }

  &__mode {
    @apply -mt-1 flex;
  }

  &__list {
    @apply flex flex-col divide-y divide-neutral-100 px-6 py-2;
  }

  &__skeletons {
    @apply flex flex-col px-6 py-2;
  }

  &__skeleton {
    @apply my-3 h-12 rounded-[--vc-radius] bg-neutral-100 motion-safe:animate-pulse;
  }

  &__caveat {
    @apply flex items-center gap-1.5 text-xs text-neutral-500;
  }

  &__notice {
    @apply flex items-start gap-2 text-sm text-neutral-600;

    // Kept in the column while empty, for screen readers; -mt-4 hands back the gap-4 it would otherwise add.
    &:empty {
      @apply -mt-4;
    }
  }

  &__notice-icon {
    @apply mt-0.5 shrink-0 text-warning;
  }
}
</style>
