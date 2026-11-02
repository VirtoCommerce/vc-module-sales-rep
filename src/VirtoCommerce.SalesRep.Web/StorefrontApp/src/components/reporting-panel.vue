<template>
  <div class="reporting-panel">
    <div class="reporting-panel__toolbar">
      <div class="reporting-panel__periods" role="group" :aria-label="t('sales_rep.reporting.period_label')">
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
      </div>

      <div v-if="$slots.actions" class="reporting-panel__actions">
        <slot name="actions" />
      </div>
    </div>

    <VcEmptyView v-if="error && !loading" :text="t('sales_rep.reporting.load_failed')" variant="error" />

    <template v-else>
      <div class="reporting-panel__tiles">
        <VcLoaderOverlay :visible="loading" />

        <ReportingKpiTile
          :label="t('sales_rep.reporting.kpi.sales', { period: periodLabel })"
          :value="formatters.compactMoney(summary.revenue, currencyCode)"
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
          :value="formatters.money(summary.periodAov, currencyCode)"
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

      <ReportingTable :months="months" :currency-code="currencyCode" :loading="loading" />

      <p class="reporting-panel__footnote">
        <VcIcon name="info" size="xs" />

        {{ footnote }}
      </p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { useLocalStorage } from "@vueuse/core";
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useReportingFormatters } from "../composables/useReportingFormatters";
import { useSalesRepReporting } from "../composables/useSalesRepReporting";
import { useToday } from "../composables/useToday";
import { REPORTING_PERIODS } from "../reporting";
import ReportingChart from "./reporting-chart.vue";
import ReportingKpiTile from "./reporting-kpi-tile.vue";
import ReportingTable from "./reporting-table.vue";
import type { ReportingPeriodType } from "../reporting";

interface IProps {
  /** One customer; empty for all the rep's customers. */
  organizationId: string;
}

const props = defineProps<IProps>();

// The owner decides where the period lives: the Reporting page keeps it in the URL.
const period = defineModel<ReportingPeriodType>("period", { required: true });

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

const { t, n, d } = useI18n();
const formatters = useReportingFormatters();
const today = useToday();

// Per browser, not per surface: every report a rep opens follows the same choice.
const salesWithLastYear = useLocalStorage("vc-sales-rep-reporting-sales-last-year", false);
const aovWithLastYear = useLocalStorage("vc-sales-rep-reporting-aov-last-year", false);

const { months, summary, currencyCode, loading, error, reportPeriod } = useSalesRepReporting({
  period,
  organizationId: () => props.organizationId,
});

// The tiles and charts are labelled with the period of their figures, which trail the tabs while a new one loads.
const periodLabel = computed(() => t(PERIOD_LABELS[reportPeriod.value]));

const chartPeriodTitle = computed(() =>
  t(CHART_PERIOD_TITLES[reportPeriod.value], {
    period: periodLabel.value,
    year: months.value[0]?.year ?? new Date(today.value).getFullYear(),
  }),
);

const currentMonthTile = computed(() => {
  const current = summary.value.currentMonth;
  if (!current) {
    return undefined;
  }
  const share = current.daysPassed / current.daysInMonth;
  return {
    label: formatters.sentenceCase(
      t("sales_rep.reporting.kpi.month_to_date", {
        month: formatters.month(current.year, current.month, "long"),
        day: current.daysPassed,
        days: current.daysInMonth,
      }),
    ),
    value: formatters.compactMoney(current.revenue, currencyCode.value),
    caption:
      current.projectedRevenue === null
        ? undefined
        : t("sales_rep.reporting.kpi.projected", {
            share: formatters.share(share),
            value: formatters.compactMoney(current.projectedRevenue, currencyCode.value),
          }),
    progress: share,
  };
});

const salesSubtitle = computed(() =>
  summary.value.averageRevenuePerClosedMonth === null
    ? undefined
    : t("sales_rep.reporting.sales_chart.subtitle", {
        value: formatters.money(summary.value.averageRevenuePerClosedMonth, currencyCode.value),
      }),
);

const aovSubtitle = computed(() =>
  t("sales_rep.reporting.aov_chart.subtitle_period", {
    aov: formatters.money(summary.value.periodAov, currencyCode.value),
  }),
);

const footnote = computed(() => {
  const started = months.value.filter((month) => month.state !== "upcoming");
  const first = started.at(0);
  const last = started.at(-1);
  if (!first || !last) {
    return t("sales_rep.reporting.footnote_upcoming");
  }
  const range = {
    from: formatters.monthYear(first.year, first.month),
    to: formatters.monthYear(last.year, last.month),
  };
  return last.state === "current"
    ? t("sales_rep.reporting.footnote", { ...range, today: d(today.value, { month: "short", day: "numeric" }) })
    : t("sales_rep.reporting.footnote_closed", range);
});
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.reporting-panel {
  @apply flex flex-col gap-4;

  &__toolbar {
    @apply flex flex-wrap items-center justify-between gap-3;
  }

  &__periods {
    // Hover contrast, as on the hub's chips (VCST-5890).
    --vc-tab-switch-hover-color: var(--color-neutral-900);

    // Wraps rather than scrolls: a scrolling row would clip the tabs' focus ring and the checked tab's shadow.
    @apply flex max-w-full flex-wrap gap-1;
  }

  &__actions {
    @apply flex w-full items-center gap-3;

    @media (width >= theme("screens.md")) {
      @apply w-auto;
    }
  }

  &__tiles {
    // Keeps the loader overlay's z-index inside the tiles, under the customer dropdown that opens over them.
    @apply relative isolate grid grid-cols-2 gap-3;

    @media (width >= theme("screens.lg")) {
      @apply grid-cols-4;
    }
  }

  &__footnote {
    @apply flex items-center gap-2 text-xs text-neutral-600;
  }
}
</style>
