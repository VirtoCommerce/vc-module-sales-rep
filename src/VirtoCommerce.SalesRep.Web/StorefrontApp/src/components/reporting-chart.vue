<template>
  <section class="reporting-chart" :aria-label="title">
    <div class="reporting-chart__header">
      <div class="reporting-chart__heading">
        <VcTypography tag="h2" variant="h5" class="reporting-chart__title">{{ title }}</VcTypography>

        <span v-if="subtitle" class="reporting-chart__subtitle">{{ subtitle }}</span>
      </div>

      <VcSwitch
        class="reporting-chart__last-year-switch"
        :model-value="withLastYear"
        :label="t('sales_rep.reporting.last_year')"
        :aria-label="t('sales_rep.reporting.last_year_toggle', { chart: title })"
        label-position="right"
        size="sm"
        :test-id="`${testId}-last-year-switch`"
        @change="$emit('update:withLastYear', $event)"
      />
    </div>

    <SalesRepChart
      :aria-label="title"
      :categories="categories"
      :series="series"
      bar-layout="overlap"
      :y-format="formatTick"
      :y-label="yLabel"
      :loading="loading"
      :data-test-id="`${testId}-plot`"
    >
      <template #annotation="{ index }">
        <ReportingChangeBadge :change="changeOf(months[index])" />
      </template>

      <template #placeholder>{{ t("sales_rep.reporting.upcoming") }}</template>

      <template #tooltip="{ index }">
        <strong class="reporting-chart__tooltip-title">
          {{ formatters.monthYear(months[index].year, months[index].month) }}
        </strong>

        <dl class="reporting-chart__tooltip-rows">
          <template v-for="row in tooltipRows(months[index])" :key="row.label">
            <dt class="reporting-chart__tooltip-label">{{ row.label }}</dt>

            <dd class="reporting-chart__tooltip-value">
              <VcPriceDisplay v-if="row.isMoney" :value="{ formattedAmount: row.value }" />

              <template v-else>{{ row.value }}</template>
            </dd>
          </template>
        </dl>
      </template>

      <template #legend-append>
        <li class="reporting-chart__legend-change">
          <VcBadge color="success" variant="soft" size="sm" rounded nowrap>
            ↑ {{ t("sales_rep.reporting.legend.up") }}
          </VcBadge>

          <VcBadge color="danger" variant="soft" size="sm" rounded nowrap>
            ↓ {{ t("sales_rep.reporting.legend.down") }}
          </VcBadge>

          {{ t("sales_rep.reporting.legend.vs_last_year") }}
        </li>
      </template>
    </SalesRepChart>

    <p class="reporting-chart__note">
      <VcIcon name="info" size="xs" />

      {{ t("sales_rep.reporting.cancelled_excluded") }}
    </p>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useReportingFormatters } from "../composables/useReportingFormatters";
import { lastYearBaseline } from "../reporting";
import ReportingChangeBadge from "./reporting-change-badge.vue";
import SalesRepChart from "./sales-rep-chart/sales-rep-chart.vue";
import type { ReportingMetricType, ReportingMonthStateType, ReportingMonthType } from "../reporting";
import type { ChartCategoryType, ChartSeriesType } from "./sales-rep-chart/types";

interface IEmits {
  (event: "update:withLastYear", value: boolean): void;
}

interface IProps {
  kind: ReportingMetricType;
  title: string;
  subtitle?: string;
  months: ReportingMonthType[];
  currencyCode: string;
  withLastYear?: boolean;
  loading?: boolean;
  testId: string;
}

interface ITooltipRow {
  label: string;
  value: string;
  /** A formatted amount, shown through VcPriceDisplay like every price in the storefront. */
  isMoney?: boolean;
}

defineEmits<IEmits>();
const props = defineProps<IProps>();

const { t } = useI18n();
const formatters = useReportingFormatters();

function valueOf(month: ReportingMonthType): number | null {
  return props.kind === "aov" ? month.aov : month.revenue;
}

function changeOf(month: ReportingMonthType): number | null {
  return props.kind === "aov" ? month.aovChange : month.revenueChange;
}

function formatTick(value: number): string {
  return formatters.compactMoney(value, props.currencyCode);
}

const yLabel = computed(() =>
  props.kind === "aov"
    ? t("sales_rep.reporting.axis.aov", { currency: formatters.currencySymbol(props.currencyCode) })
    : undefined,
);

const categories = computed<ChartCategoryType[]>(() =>
  props.months.map((month) => {
    const isUpcoming = month.state === "upcoming";
    // The trailing dot marks a month that has not started, as in the design.
    const mark = isUpcoming ? " ·" : "";
    return {
      id: `${month.year}-${month.month}`,
      label: `${formatters.month(month.year, month.month, "short")}${mark}`,
      shortLabel: `${formatters.month(month.year, month.month, "narrow")}${mark}`,
      placeholder: isUpcoming,
    };
  }),
);

// Closed months and the current one are separate bar series, so each has its own colour and legend entry; they never
// share a column. The projection outline sits under the current month's bar.
const series = computed<ChartSeriesType[]>(() => {
  const inState = (state: ReportingMonthStateType) =>
    props.months.map((month) => (month.state === state ? valueOf(month) : null));
  const isAov = props.kind === "aov";

  return [
    {
      id: "closed",
      type: "bar",
      name: t(isAov ? "sales_rep.reporting.legend.aov_closed" : "sales_rep.reporting.legend.sales_closed"),
      color: isAov ? "neutral-300" : "primary-200",
      data: inState("closed"),
    },
    {
      id: "current",
      type: "bar",
      name: t("sales_rep.reporting.legend.current"),
      color: "primary-500",
      data: inState("current"),
    },
    {
      id: "projection",
      type: "bar",
      variant: "dashed",
      name: t("sales_rep.reporting.legend.projection"),
      color: "primary-500",
      data: props.months.map((month) => month.projectedRevenue),
      hidden: isAov,
    },
    {
      id: "last-year",
      type: "line",
      name: t("sales_rep.reporting.legend.last_year"),
      color: "accent-500",
      curve: "monotone",
      dots: true,
      data: props.months.map((month) => (isAov ? month.lastYearAov : month.lastYearRevenue)),
      hidden: !props.withLastYear,
    },
  ];
});

/** Label/value rows: the values line up in one right-aligned column so they read top to bottom. */
function tooltipRows(month: ReportingMonthType): ITooltipRow[] {
  const money = (value: number | null) => formatters.money(value, props.currencyCode);
  const rows: ITooltipRow[] = [];

  if (props.kind === "aov") {
    rows.push({ label: t("sales_rep.reporting.tooltip.aov"), value: money(month.aov), isMoney: month.aov !== null });
  } else {
    rows.push({
      label:
        month.state === "current"
          ? t("sales_rep.reporting.tooltip.revenue_to_date")
          : t("sales_rep.reporting.tooltip.revenue"),
      value: money(month.revenue),
      isMoney: month.revenue !== null,
    });
    if (month.projectedRevenue !== null) {
      rows.push({
        label: t("sales_rep.reporting.tooltip.projection"),
        value: money(month.projectedRevenue),
        isMoney: true,
      });
    }
  }

  const monthChange = props.kind === "aov" ? month.aovMonthChange : month.revenueMonthChange;
  if (monthChange !== null) {
    rows.push({
      label: t("sales_rep.reporting.tooltip.vs_month", {
        month: formatters.month(month.year, month.month - 1, "short"),
      }),
      value: formatters.signedChange(monthChange),
    });
  }

  const lastYearMonth = formatters.monthYear(month.year - 1, month.month);
  const yearChange = changeOf(month);
  if (yearChange !== null) {
    rows.push({
      label: t("sales_rep.reporting.tooltip.vs_month", { month: lastYearMonth }),
      value: formatters.signedChange(yearChange),
    });
  }

  const baseline = lastYearBaseline(month, props.kind);
  rows.push({
    label:
      props.kind === "aov"
        ? t("sales_rep.reporting.tooltip.last_year_aov", { month: lastYearMonth })
        : t("sales_rep.reporting.tooltip.last_year_revenue", { month: lastYearMonth }),
    value: money(baseline),
    isMoney: baseline !== null,
  });

  return rows;
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.reporting-chart {
  @apply flex flex-col gap-3 rounded-[--vc-radius] border border-neutral-200 p-3;

  @media (width >= theme("screens.md")) {
    @apply p-4;
  }

  &__header {
    @apply flex w-full flex-wrap items-center justify-between gap-3;
  }

  &__heading {
    @apply flex flex-wrap items-baseline gap-x-3 gap-y-1;
  }

  &__subtitle {
    @apply text-sm text-neutral-600;
  }

  &__last-year-switch {
    @apply ms-auto;
  }

  &__tooltip-title {
    @apply font-bold;
  }

  &__tooltip-rows {
    @apply mt-1 grid grid-cols-[1fr_auto] gap-x-4 gap-y-0.5;
  }

  &__tooltip-label {
    @apply whitespace-nowrap text-neutral-600;
  }

  &__tooltip-value {
    @apply text-end font-bold tabular-nums;
  }

  &__legend-change {
    @apply flex items-center gap-2;
  }

  &__note {
    @apply flex items-center gap-2 border-t border-neutral-200 pt-3 text-xs text-neutral-600;
  }
}
</style>
