<template>
  <div class="sales-rep-reporting">
    <VcTypography class="sales-rep-reporting__title" tag="h1">
      {{ t("sales_rep.reporting.title") }}
    </VcTypography>

    <p class="sales-rep-reporting__subtitle">{{ t("sales_rep.reporting.subtitle") }}</p>

    <VcWidget class="sales-rep-reporting__card">
      <template #header>
        <div class="sales-rep-reporting__card-header">
          <div class="sales-rep-reporting__card-heading">
            <VcTypography tag="h2" variant="h6" class="sales-rep-reporting__card-title">
              {{ t("sales_rep.reporting.card_title") }}
            </VcTypography>

            <VcBadge v-if="isMock" color="neutral" variant="outline" size="sm" rounded nowrap>
              {{ t("sales_rep.reporting.example_data") }}
            </VcBadge>
          </div>

          <VcLink class="sales-rep-reporting__all-orders" :to="{ name: ALL_CUSTOMER_ORDERS_ROUTE_NAME }">
            {{ t("sales_rep.reporting.all_orders") }}

            <VcIcon name="arrow-right" size="xs" />
          </VcLink>
        </div>
      </template>

      <div class="sales-rep-reporting__body">
        <div class="sales-rep-reporting__toolbar">
          <VcTabSwitchGroup
            class="sales-rep-reporting__periods"
            variant="seg"
            :aria-label="t('sales_rep.reporting.period_label')"
          >
            <VcTabSwitch
              v-for="option in REPORTING_PERIODS"
              :key="option"
              :model-value="period"
              name="sales-rep-reporting-period"
              :value="option"
              :label="t(PERIOD_LABELS[option])"
              size="sm"
              @change="period = $event"
            />
          </VcTabSwitchGroup>

          <div class="sales-rep-reporting__actions">
            <VcSelect
              class="sales-rep-reporting__customer"
              :model-value="customerId"
              :items="customerItems"
              :aria-label="t('sales_rep.reporting.customer_label')"
              :disabled="customersLoading"
              :error="customersFailed"
              :message="customersFailed ? t('sales_rep.reporting.customers_failed') : undefined"
              text-field="organizationName"
              value-field="organizationId"
              size="sm"
              test-id-dropdown="sales-rep-reporting-customer-select"
              autocomplete
              @change="customerId = $event ?? ALL_CUSTOMERS"
            />
          </div>
        </div>

        <VcEmptyView v-if="error && !loading" :text="t('sales_rep.reporting.load_failed')" variant="error" />

        <template v-else>
          <div class="sales-rep-reporting__tiles">
            <ReportingKpiTile
              :label="t('sales_rep.reporting.kpi.sales', { period: periodLabel })"
              :value="compactMoney(summary.revenue)"
              :change="summary.revenueChange"
              test-id="sales-rep-reporting-sales-tile"
            />

            <ReportingKpiTile
              :label="t('sales_rep.reporting.kpi.orders', { period: periodLabel })"
              :value="n(summary.orders)"
              :change="summary.ordersChange"
              test-id="sales-rep-reporting-orders-tile"
            />

            <ReportingKpiTile
              :label="t('sales_rep.reporting.kpi.aov', { period: periodLabel })"
              :value="money(summary.periodAov)"
              :change="summary.aovChange"
              test-id="sales-rep-reporting-aov-tile"
            />

            <ReportingKpiTile
              v-if="currentMonthTile"
              :label="currentMonthTile.label"
              :value="currentMonthTile.value"
              :caption="currentMonthTile.caption"
              :progress="currentMonthTile.progress"
              test-id="sales-rep-reporting-month-tile"
            />
          </div>

          <ReportingChart
            v-model:with-last-year="salesWithLastYear"
            kind="sales"
            :title="t('sales_rep.reporting.sales_chart.title', { period: chartPeriodTitle })"
            :subtitle="salesSubtitle"
            :months="months"
            :currency-code="currencyCode"
            :loading="loading"
            test-id="sales-rep-reporting-sales"
          />

          <ReportingChart
            v-model:with-last-year="aovWithLastYear"
            kind="aov"
            :title="t('sales_rep.reporting.aov_chart.title')"
            :subtitle="aovSubtitle"
            :months="months"
            :currency-code="currencyCode"
            :loading="loading"
            test-id="sales-rep-reporting-aov"
          />

          <ReportingTable :months="months" :currency-code="currencyCode" />

          <p class="sales-rep-reporting__footnote">
            <VcIcon name="info" size="xs" />

            {{ footnote }}
          </p>
        </template>
      </div>
    </VcWidget>
  </div>
</template>

<script setup lang="ts">
import { useLocalStorage } from "@vueuse/core";
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useBreadcrumbs, usePageHead, useRouteQueryParam } from "@vc-frontend/core";
import ReportingChart from "../components/reporting-chart.vue";
import ReportingKpiTile from "../components/reporting-kpi-tile.vue";
import ReportingTable from "../components/reporting-table.vue";
import { useSalesRepCustomerOptions } from "../composables/useSalesRepCustomerOptions";
import { useSalesRepReporting } from "../composables/useSalesRepReporting";
import { ALL_CUSTOMER_ORDERS_ROUTE_NAME, DASHBOARD_ROUTE_NAME } from "../constants";
import { REPORTING_DEFAULT_PERIOD, REPORTING_PERIODS } from "../reporting";
import type { ReportingPeriodType } from "../reporting";

const PERIOD_LABELS: Record<ReportingPeriodType, string> = {
  "6m": "sales_rep.reporting.periods.six_months",
  "12m": "sales_rep.reporting.periods.twelve_months",
  year: "sales_rep.reporting.periods.year",
  q1: "sales_rep.reporting.periods.q1",
  q2: "sales_rep.reporting.periods.q2",
  q3: "sales_rep.reporting.periods.q3",
  q4: "sales_rep.reporting.periods.q4",
};

// The chart heading reads "Monthly sales · last 12 months", so rolling periods get their own wording.
const CHART_PERIOD_TITLES: Record<ReportingPeriodType, string> = {
  "6m": "sales_rep.reporting.chart_periods.six_months",
  "12m": "sales_rep.reporting.chart_periods.twelve_months",
  year: "sales_rep.reporting.chart_periods.year",
  q1: "sales_rep.reporting.chart_periods.quarter",
  q2: "sales_rep.reporting.chart_periods.quarter",
  q3: "sales_rep.reporting.chart_periods.quarter",
  q4: "sales_rep.reporting.chart_periods.quarter",
};

const ALL_CUSTOMERS = "";

const { t, n, d } = useI18n();

const period = useRouteQueryParam<ReportingPeriodType>("period", {
  defaultValue: REPORTING_DEFAULT_PERIOD,
  validator: (value) => REPORTING_PERIODS.includes(value),
  updateMethod: "replace",
});

const customerId = useRouteQueryParam<string>("customer", { defaultValue: ALL_CUSTOMERS, updateMethod: "replace" });

const salesWithLastYear = useLocalStorage("vc-sales-rep-reporting-sales-last-year", false);
const aovWithLastYear = useLocalStorage("vc-sales-rep-reporting-aov-last-year", false);

const { options: customers, loading: customersLoading, failed: customersFailed } = useSalesRepCustomerOptions();

const customerItems = computed(() => [
  {
    organizationId: ALL_CUSTOMERS,
    organizationName: t("sales_rep.reporting.all_customers", { count: n(customers.value.length) }),
  },
  ...customers.value,
]);

const { months, summary, currencyCode, loading, error, isMock } = useSalesRepReporting({
  period,
  organizationId: customerId,
});

function money(value: number | null): string {
  return value === null ? "—" : n(value, { style: "currency", currency: currencyCode.value, maximumFractionDigits: 0 });
}

function compactMoney(value: number | null): string {
  return value === null
    ? "—"
    : n(value, { style: "currency", currency: currencyCode.value, notation: "compact", maximumFractionDigits: 1 });
}

const periodLabel = computed(() => t(PERIOD_LABELS[period.value]));

const firstMonth = computed(() => months.value[0]);

const chartPeriodTitle = computed(() =>
  t(CHART_PERIOD_TITLES[period.value], {
    period: periodLabel.value,
    year: firstMonth.value?.year ?? new Date().getFullYear(),
  }),
);

const currentMonthTile = computed(() => {
  const current = summary.value.currentMonth;
  if (!current) {
    return undefined;
  }
  const projected = current.projectedRevenue;
  return {
    label: t("sales_rep.reporting.kpi.month_to_date", {
      month: d(new Date(current.year, current.month, 1), { month: "long" }),
      day: current.daysPassed,
      days: current.daysInMonth,
    }),
    value: compactMoney(current.revenue),
    caption:
      projected === null
        ? undefined
        : t("sales_rep.reporting.kpi.projected", {
            share: n(current.daysPassed / current.daysInMonth, { style: "percent" }),
            value: compactMoney(projected),
          }),
    progress: current.daysPassed / current.daysInMonth,
  };
});

const salesSubtitle = computed(() =>
  summary.value.averageRevenuePerClosedMonth === null
    ? undefined
    : t("sales_rep.reporting.sales_chart.subtitle", { value: money(summary.value.averageRevenuePerClosedMonth) }),
);

const aovSubtitle = computed(() =>
  t("sales_rep.reporting.aov_chart.subtitle_period", { aov: money(summary.value.periodAov) }),
);

const footnote = computed(() => {
  const withData = months.value.filter((month) => month.state !== "upcoming");
  const first = withData[0];
  const last = withData.at(-1);
  if (!first || !last) {
    return t("sales_rep.reporting.footnote_empty");
  }
  const monthYear = (year: number, month: number): string =>
    d(new Date(year, month, 1), { month: "short", year: "numeric" });
  return t("sales_rep.reporting.footnote", {
    from: monthYear(first.year, first.month),
    to: monthYear(last.year, last.month),
    today: d(new Date(), { month: "short", day: "numeric" }),
  });
});

usePageHead({
  title: computed(() => t("sales_rep.reporting.title")),
});

useBreadcrumbs(() => [
  { title: t("common.links.account"), route: { name: "Account" } },
  { title: t("sales_rep.hub.title"), route: { name: DASHBOARD_ROUTE_NAME } },
  { title: t("sales_rep.reporting.title") },
]);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-reporting {
  @apply flex flex-col gap-5;

  &__title {
    @apply [word-break:break-word];
  }

  &__subtitle {
    @apply -mt-4 text-sm text-neutral-600;
  }

  &__card-header {
    @apply flex w-full items-center justify-between gap-3;
  }

  &__card-heading {
    @apply flex items-center gap-2;
  }

  &__card-title {
    @apply uppercase;
  }

  &__all-orders {
    @apply inline-flex items-center gap-1 text-sm font-bold;
  }

  &__body {
    @apply flex flex-col gap-4;
  }

  &__toolbar {
    @apply flex flex-wrap items-center justify-between gap-3;
  }

  &__periods {
    @apply max-w-full overflow-x-auto;
  }

  &__actions {
    @apply flex w-full items-center gap-3;

    @media (width >= theme("screens.md")) {
      @apply w-auto;
    }
  }

  &__customer {
    @apply min-w-0 flex-1;

    @media (width >= theme("screens.md")) {
      @apply w-64 flex-none;
    }
  }

  &__tiles {
    @apply grid grid-cols-2 gap-3;

    @media (width >= theme("screens.lg")) {
      @apply grid-cols-4;
    }
  }

  &__footnote {
    @apply flex items-center gap-2 text-xs text-neutral-600;
  }
}
</style>
