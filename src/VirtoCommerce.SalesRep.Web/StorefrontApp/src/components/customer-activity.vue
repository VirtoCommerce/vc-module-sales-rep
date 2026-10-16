<template>
  <LayoutWidget :title="t('sales_rep.activity.customer.title')" size="md" class="customer-activity">
    <template #append>
      <VcLink
        :to="{ name: CUSTOMER_ACTIVITIES_ROUTE_NAME, params: { organizationId } }"
        class="customer-activity__all-link"
        data-test-id="customer-activity-all-link"
      >
        {{ t("sales_rep.activity.view_all") }}

        <VcIcon name="arrow-right" size="xs" />
      </VcLink>
    </template>

    <template #default-container>
      <!-- The summary plus the two insights lists, as sub-views behind these chips (baseline = Summary). -->
      <div class="customer-activity__views">
        <SalesRepRuleChips
          v-model="viewChip"
          :rules="viewChipRules"
          :all-label="t('sales_rep.activity.customer.views.summary')"
        />
      </div>

      <div v-show="!viewChip" class="customer-activity__summary" data-test-id="customer-activity-summary-section">
        <VcEmptyView v-if="failed && !loading" :text="t('sales_rep.activity.customer.load_failed')" variant="error" />

        <VcEmptyView v-else-if="!summary && !loading" :text="t('sales_rep.activity.customer.empty')" icon="activity" />

        <!-- Same first-load skeleton as the insights widgets — the GA-backed query can run for seconds. -->
        <div v-else-if="loading && !summary" class="customer-activity__skeletons" aria-hidden="true">
          <div
            v-for="index in skeletonRows"
            :key="index"
            class="customer-activity__skeleton"
            data-test-id="customer-activity-skeleton"
          />
        </div>

        <template v-else-if="summary">
          <dl class="customer-activity__list">
            <div v-if="summary.createdOn" class="customer-activity__row" data-test-id="customer-activity-row">
              <dt class="customer-activity__label">
                <VcIcon class="customer-activity__icon" name="calendar" :size="15" />
                {{ t("sales_rep.activity.customer.created_on") }}
              </dt>

              <dd class="customer-activity__value">{{ $d(new Date(summary.createdOn)) }}</dd>
            </div>

            <template v-if="summary.isAnalyticsAvailable">
              <div class="customer-activity__row" data-test-id="customer-activity-row">
                <dt class="customer-activity__label">
                  <VcIcon class="customer-activity__icon" name="log-in" :size="15" />
                  {{ t("sales_rep.activity.customer.last_web_login") }}
                  <TrackedMetricHint />
                </dt>

                <dd class="customer-activity__value">
                  {{ summary.lastWebLogin ? $d(new Date(summary.lastWebLogin)) : "—" }}
                </dd>
              </div>

              <div class="customer-activity__row" data-test-id="customer-activity-row">
                <dt class="customer-activity__label">
                  <VcIcon class="customer-activity__icon" name="activity" :size="15" />
                  {{ t("sales_rep.activity.customer.visits") }}
                  <TrackedMetricHint />
                </dt>

                <dd class="customer-activity__value">{{ formatStatCount(summary.visitsCount) }}</dd>
              </div>

              <div class="customer-activity__row" data-test-id="customer-activity-row">
                <dt class="customer-activity__label">
                  <VcIcon class="customer-activity__icon" name="search" :size="15" />
                  {{ t("sales_rep.activity.customer.last_search_term") }}
                  <TrackedMetricHint />
                </dt>

                <dd class="customer-activity__value">
                  <VcLink
                    v-if="summary.lastSearchTerm"
                    class="customer-activity__link"
                    :to="searchRoute"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {{ t("sales_rep.activity.quoted_term", { term: summary.lastSearchTerm }) }}
                  </VcLink>

                  <template v-else>—</template>

                  <span
                    v-if="summary.lastSearchedDate"
                    class="customer-activity__date"
                    data-test-id="customer-activity-date-label"
                  >
                    {{ $d(new Date(summary.lastSearchedDate)) }}
                  </span>
                </dd>
              </div>

              <!-- A product goes under its label: beside it, a long name got a ~150 px column and ran to 8 lines.
                   The dash stays in line with the other rows' values. -->
              <div
                class="customer-activity__row"
                :class="{ 'customer-activity__row--stacked': summary.lastViewedProduct }"
                data-test-id="customer-activity-row"
              >
                <dt class="customer-activity__label">
                  <VcIcon class="customer-activity__icon" name="eye" :size="15" />
                  {{ t("sales_rep.activity.customer.last_viewed_product") }}
                  <TrackedMetricHint />
                </dt>

                <dd class="customer-activity__value">
                  <template v-if="summary.lastViewedProduct">
                    <VcLink
                      v-if="productRoute"
                      class="customer-activity__link customer-activity__product"
                      :to="productRoute"
                      :title="productLabel"
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      {{ productLabel }}
                    </VcLink>

                    <span v-else class="customer-activity__product" :title="productLabel">{{ productLabel }}</span>
                  </template>

                  <template v-else>—</template>

                  <span
                    v-if="summary.lastViewedDate"
                    class="customer-activity__date"
                    data-test-id="customer-activity-date-label"
                  >
                    {{ $d(new Date(summary.lastViewedDate)) }}
                  </span>
                </dd>
              </div>
            </template>
          </dl>

          <!-- Untracked or unreadable activity must not read as "never logs in / searches"; Created on (DB data)
               still shows. Outside the <dl>, which may hold only dt/dd groups. -->
          <div
            v-if="!summary.isAnalyticsAvailable"
            class="customer-activity__note"
            data-test-id="customer-activity-unavailable-label"
          >
            <VcIcon class="customer-activity__icon" name="circle-alert" :size="15" />
            {{ t("sales_rep.activity.customer.analytics_unavailable") }}
          </div>
        </template>
      </div>

      <!-- Mounted on first visit, so opening the profile fires one GA-backed query, not three; v-show and a
           kept subscription after, so a revisit refires nothing. -->
      <CustomerSearchHistory
        v-if="visitedViews.has(SEARCHES_VIEW)"
        v-show="viewChip === SEARCHES_VIEW"
        :organization-id="organizationId"
      />

      <CustomerBrowseHistory
        v-if="visitedViews.has(PRODUCT_VIEWS_VIEW)"
        v-show="viewChip === PRODUCT_VIEWS_VIEW"
        :organization-id="organizationId"
      />
    </template>
  </LayoutWidget>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { getProductRoute } from "@vc-frontend/core";
import { useSalesRepCustomerActivitySummary } from "../composables/useSalesRepCustomerActivitySummary";
import { CUSTOMER_ACTIVITIES_ROUTE_NAME } from "../constants";
import { formatStatCount, searchResultsRoute } from "../utils";
import CustomerBrowseHistory from "./customer-browse-history.vue";
import CustomerSearchHistory from "./customer-search-history.vue";
import LayoutWidget from "./layout-widget.vue";
import SalesRepRuleChips from "./sales-rep-rule-chips.vue";
import TrackedMetricHint from "./tracked-metric-hint.vue";
import type { SalesRepRuleType } from "../types";

interface IProps {
  organizationId: string;
}

const props = defineProps<IProps>();

const { t } = useI18n();

const SEARCHES_VIEW = "searches";
const PRODUCT_VIEWS_VIEW = "product_views";

// undefined = the baseline Summary view, per the rule-chips convention.
const viewChip = ref<string | undefined>(undefined);
const viewChipRules = computed<SalesRepRuleType[]>(() => [
  { name: SEARCHES_VIEW, label: t("sales_rep.activity.customer.views.searches") },
  { name: PRODUCT_VIEWS_VIEW, label: t("sales_rep.activity.customer.views.product_views") },
]);

const visitedViews = reactive(new Set<string>());
watch(viewChip, (view) => {
  if (view) {
    visitedViews.add(view);
  }
});

// The profile page keeps this instance when it moves to another customer, so the widget starts over on Summary:
// otherwise it opens on the previous customer's sub-view, and the visited panels query the new one at once.
watch(
  () => props.organizationId,
  () => {
    viewChip.value = undefined;
    visitedViews.clear();
  },
);

// Lifetime, unlike the tabs beside it (this year): a "last" fact must survive a quiet year, so it shows its date.
const { summary, loading, error } = useSalesRepCustomerActivitySummary(() => props.organizationId);

// One skeleton bar per definition row the loaded widget renders.
const skeletonRows = 5;

const failed = computed(() => Boolean(error.value));

const searchRoute = computed(() => searchResultsRoute(summary.value?.lastSearchTerm ?? ""));

// By id: /product/{id} always resolves; an unresolved code has no id and stays plain text.
const productRoute = computed(() => {
  const productId = summary.value?.lastViewedProduct?.productId;
  return productId ? getProductRoute(productId) : undefined;
});

// Code + name per the design; unresolvable codes still show the code alone.
const productLabel = computed(() => {
  const product = summary.value?.lastViewedProduct;
  if (!product) {
    return "";
  }
  return product.name ? `${product.code} · ${product.name}` : product.code;
});
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-activity {
  $stackedRow: "";

  // px-6 aligns the chips with the widget header title, like the panels' own sort-chip rows.
  &__views {
    @apply border-b border-neutral-200 bg-neutral-50 px-6 py-3;
  }

  // The same px-6 as the chips above and the panels, so the card keeps one left edge on every screen.
  &__summary {
    @apply px-6 pb-5 pt-4;
  }

  &__skeletons {
    @apply flex flex-col;
  }

  // h-6 + my-3 ≈ one loaded definition row (a py-3 text line), so the card keeps its height.
  &__skeleton {
    @apply my-3 h-6 rounded-[--vc-radius] bg-neutral-100 motion-safe:animate-pulse;
  }

  &__row {
    @apply flex items-baseline justify-between gap-4 border-b border-neutral-100 py-3 last:border-b-0;

    &--stacked {
      $stackedRow: &;

      @apply flex-col items-stretch gap-1;
    }
  }

  &__label {
    @apply flex flex-none items-center gap-2 text-sm text-neutral-500;
  }

  &__icon {
    @apply text-neutral-400;
  }

  &__value {
    @apply min-w-0 text-end text-sm text-neutral-900 [word-break:break-word];

    #{$stackedRow} & {
      @apply text-start;
    }
  }

  &__link {
    @apply text-[--link-color] hover:underline;
  }

  // Two lines at most; the full name stays in the title.
  &__product {
    @apply line-clamp-2;
  }

  // Under the value, so a long product name keeps the whole first line.
  &__date {
    @apply block text-xs text-neutral-500;
  }

  &__note {
    @apply flex items-start gap-2 py-3 text-sm text-neutral-500;
  }

  // The note sits outside the list, so it draws the divider the last row used to.
  &__list + &__note {
    @apply border-t border-neutral-100;
  }

  &__all-link {
    @apply inline-flex items-center gap-1 whitespace-nowrap text-sm text-[--link-color] hover:text-[--link-hover-color];
  }
}
</style>
