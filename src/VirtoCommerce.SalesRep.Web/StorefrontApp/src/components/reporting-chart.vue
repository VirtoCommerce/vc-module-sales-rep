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
        label-position="right"
        size="sm"
        :test-id="`${testId}-last-year-switch`"
        @change="$emit('update:withLastYear', $event)"
      />
    </div>

    <div ref="plot" class="reporting-chart__plot" :data-test-id="`${testId}-plot`">
      <VcLoaderOverlay :visible="loading" no-bg />

      <svg
        v-if="width > 0"
        class="reporting-chart__svg"
        :width="width"
        :height="HEIGHT"
        :viewBox="`0 0 ${width} ${HEIGHT}`"
        role="group"
        :aria-label="title"
      >
        <g class="reporting-chart__grid">
          <g v-for="tick in leftTicks" :key="`l${tick}`">
            <line :x1="MARGIN.left" :x2="width - marginRight" :y1="leftY(tick)" :y2="leftY(tick)" />

            <text class="reporting-chart__tick" :x="MARGIN.left - 8" :y="leftY(tick)" text-anchor="end" dy="0.32em">
              {{ formatLeftTick(tick) }}
            </text>
          </g>

          <template v-if="kind === 'aov'">
            <text class="reporting-chart__axis-title" :x="MARGIN.left - 8" :y="AXIS_TITLE_Y" text-anchor="end">
              {{ t("sales_rep.reporting.axis.aov", { currency: currencySymbol }) }}
            </text>
          </template>
        </g>

        <g v-for="(month, i) in months" :key="`${month.year}-${month.month}`">
          <template v-if="month.state === 'upcoming'">
            <rect
              class="reporting-chart__upcoming"
              :x="barX(i)"
              :y="upcomingTop"
              :width="barWidth"
              :height="MARGIN.top + plotHeight - upcomingTop"
              :rx="OUTLINE_RADIUS"
            />

            <!-- Across, as in the design; only a phone-narrow column turns it upright so it stays readable. -->
            <text
              :class="['reporting-chart__upcoming-label', { 'reporting-chart__upcoming-label--small': !isLabelWide }]"
              :x="centerX(i)"
              :y="upcomingLabelY"
              :transform="isLabelUpright ? `rotate(-90 ${centerX(i)} ${upcomingLabelY})` : undefined"
              text-anchor="middle"
              dy="0.32em"
            >
              {{ t("sales_rep.reporting.upcoming") }}
            </text>
          </template>

          <template v-else>
            <rect
              v-if="projectedBar(month) !== null"
              class="reporting-chart__projection"
              :x="barX(i)"
              :y="leftY(projectedBar(month) ?? 0)"
              :width="barWidth"
              :height="barHeight(projectedBar(month) ?? 0)"
              :rx="OUTLINE_RADIUS"
            />

            <rect
              :class="[
                'reporting-chart__bar',
                {
                  'reporting-chart__bar--current': month.state === 'current',
                  'reporting-chart__bar--aov': kind === 'aov',
                  'reporting-chart__bar--active': activeIndex === i,
                },
              ]"
              :x="barX(i)"
              :y="leftY(barValue(month) ?? 0)"
              :width="barWidth"
              :height="barHeight(barValue(month) ?? 0)"
            />
          </template>

          <text
            :class="['reporting-chart__month', { 'reporting-chart__month--upcoming': month.state === 'upcoming' }]"
            :x="centerX(i)"
            :y="HEIGHT - 8"
            text-anchor="middle"
          >
            {{ monthLabel(month) }}
          </text>
        </g>

        <template v-if="withLastYear">
          <path class="reporting-chart__last-year-line" :d="smoothLinePath(lastYearPoints)" />

          <circle
            v-for="point in lastYearPoints"
            :key="`ly${point.x}`"
            class="reporting-chart__last-year-dot"
            :cx="point.x"
            :cy="point.y"
            r="3.5"
          />
        </template>
      </svg>

      <!-- Kit components cannot live inside an SVG, so the YoY badges and the tooltip targets are laid over it,
           positioned on the same scale. -->
      <template v-if="width > 0">
        <VcBadge
          v-for="pill in pills"
          :key="pill.key"
          class="reporting-chart__pill"
          :style="{ left: `${pill.x}px`, top: `${pill.y}px` }"
          :color="pill.isDown ? 'danger' : 'success'"
          variant="soft"
          size="sm"
          rounded
          nowrap
        >
          {{ pill.text }}
        </VcBadge>

        <VcTooltip
          v-for="target in hoverTargets"
          :key="`hit-${target.month.year}-${target.month.month}`"
          class="reporting-chart__hit"
          :style="{ left: `${MARGIN.left + target.index * band}px`, top: `${target.top}px`, width: `${band}px` }"
          placement="top"
          hover
          @shown="onTooltipShown(target.index, $event)"
        >
          <template #default="{ triggerProps }">
            <div
              class="reporting-chart__hit-area"
              :style="{ height: `${target.height}px` }"
              tabindex="0"
              v-bind="triggerProps"
              :aria-label="tooltipAriaLabel(target.month)"
            />
          </template>

          <template #content>
            <strong class="reporting-chart__tooltip-title">{{ tooltipTitle(target.month) }}</strong>

            <dl class="reporting-chart__tooltip-rows">
              <template v-for="row in tooltipRows(target.month)" :key="row.label">
                <dt class="reporting-chart__tooltip-label">{{ row.label }}</dt>

                <dd class="reporting-chart__tooltip-value">
                  <VcPriceDisplay v-if="row.isMoney" :value="{ formattedAmount: row.value }" />

                  <template v-else>{{ row.value }}</template>
                </dd>
              </template>
            </dl>
          </template>
        </VcTooltip>
      </template>
    </div>

    <ul class="reporting-chart__legend">
      <li class="reporting-chart__legend-item">
        <span :class="['reporting-chart__swatch', { 'reporting-chart__swatch--aov': kind === 'aov' }]" />
        {{ kind === "aov" ? t("sales_rep.reporting.legend.aov_closed") : t("sales_rep.reporting.legend.sales_closed") }}
      </li>

      <li class="reporting-chart__legend-item">
        <span class="reporting-chart__swatch reporting-chart__swatch--current" />
        {{ t("sales_rep.reporting.legend.current") }}
      </li>

      <li v-if="kind === 'sales'" class="reporting-chart__legend-item">
        <span class="reporting-chart__swatch reporting-chart__swatch--projection" />
        {{ t("sales_rep.reporting.legend.projection") }}
      </li>

      <li v-if="withLastYear" class="reporting-chart__legend-item">
        <span class="reporting-chart__line reporting-chart__line--last-year" />
        {{ t("sales_rep.reporting.legend.last_year") }}
      </li>

      <li class="reporting-chart__legend-item">
        <VcBadge color="success" variant="soft" size="sm" rounded nowrap>
          ↑ {{ t("sales_rep.reporting.legend.up") }}
        </VcBadge>

        <VcBadge color="danger" variant="soft" size="sm" rounded nowrap>
          ↓ {{ t("sales_rep.reporting.legend.down") }}
        </VcBadge>
        {{ t("sales_rep.reporting.legend.vs_last_year") }}
      </li>
    </ul>

    <p class="reporting-chart__note">
      <VcIcon name="info" size="xs" />

      {{ t("sales_rep.reporting.cancelled_excluded") }}
    </p>
  </section>
</template>

<script setup lang="ts">
import { useResizeObserver } from "@vueuse/core";
import { computed, ref, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { niceTicks, smoothLinePath } from "../reporting";
import type { ReportingMonthType } from "../reporting";

interface IEmits {
  (event: "update:withLastYear", value: boolean): void;
}

interface ITooltipRow {
  label: string;
  value: string;
  /** A formatted amount, shown through VcPriceDisplay like every price in the storefront. */
  isMoney?: boolean;
}

interface IProps {
  kind: "sales" | "aov";
  title: string;
  subtitle?: string;
  months: ReportingMonthType[];
  currencyCode: string;
  withLastYear?: boolean;
  loading?: boolean;
  testId: string;
}

defineEmits<IEmits>();
const props = defineProps<IProps>();

type PointType = { x: number; y: number };

// The design keeps both charts compact: about 150px of plot under the badges.
const HEIGHT = 230;
const MARGIN = { top: 34, left: 64, right: 16, bottom: 30 };
// VcBadge size="sm" height and its gap above the bar.
const PILL = { height: 22, gap: 6 };
// Below this column width pills, "Upcoming" and full month names no longer fit.
const WIDE_BAND = 52;
// "Upcoming" fits across a column this wide at the regular size, and at the small size down to UPRIGHT_BAND.
const LABEL_BAND = 76;
const UPRIGHT_BAND = 40;
// An upcoming month is a dashed placeholder this share of the plot high, as in the design.
const UPCOMING_SHARE = 0.45;
// Dashed outlines (the projected month end, upcoming months) are rounded like the design's; bars stay square.
const OUTLINE_RADIUS = 4;
// Columns nearly touch, as in the design.
const BAR_SHARE = 0.92;
const AXIS_TITLE_Y = 12;
// Even an empty month keeps a hover target this tall above the baseline.
const MIN_HIT_HEIGHT = 12;

const { t, n, d } = useI18n();

const plot = useTemplateRef<HTMLElement>("plot");
const width = ref(0);
const activeIndex = ref<number | null>(null);
// "AOV, $": the symbol alone, taken from the formatted zero so it follows the store currency and locale.
const currencySymbol = computed(() =>
  n(0, { style: "currency", currency: props.currencyCode, maximumFractionDigits: 0 }).replace(/[0\s\u00a0]/g, ""),
);

useResizeObserver(plot, (entries) => {
  width.value = Math.floor(entries[0]?.contentRect.width ?? 0);
});

const marginRight = computed(() => MARGIN.right);
const plotHeight = computed(() => HEIGHT - MARGIN.top - MARGIN.bottom);
const band = computed(() => (width.value - MARGIN.left - marginRight.value) / Math.max(props.months.length, 1));
const barWidth = computed(() => band.value * BAR_SHARE);
const isWide = computed(() => band.value >= WIDE_BAND);
const isLabelWide = computed(() => band.value >= LABEL_BAND);
const isLabelUpright = computed(() => band.value < UPRIGHT_BAND);
const upcomingHeight = computed(() => plotHeight.value * UPCOMING_SHARE);
const upcomingTop = computed(() => MARGIN.top + plotHeight.value - upcomingHeight.value);
// The caption sits in the middle of its placeholder, across or (phone-narrow) upright.
const upcomingLabelY = computed(() => upcomingTop.value + upcomingHeight.value / 2);

function barValue(month: ReportingMonthType): number | null {
  return props.kind === "aov" ? month.aov : month.revenue;
}

function projectedBar(month: ReportingMonthType): number | null {
  return props.kind === "sales" ? month.projectedRevenue : null;
}

function lastYearValue(month: ReportingMonthType): number | null {
  return props.kind === "aov" ? month.lastYearAov : month.lastYearRevenue;
}

function change(month: ReportingMonthType): number | null {
  return props.kind === "aov" ? month.aovChange : month.revenueChange;
}

const leftTicks = computed(() => {
  const values = props.months.flatMap((month) => [
    barValue(month) ?? 0,
    projectedBar(month) ?? 0,
    props.withLastYear ? (lastYearValue(month) ?? 0) : 0,
  ]);
  return niceTicks(Math.max(0, ...values));
});

function scale(value: number, ticks: number[]): number {
  const max = ticks.at(-1) ?? 1;
  return MARGIN.top + plotHeight.value - (value / max) * plotHeight.value;
}

function leftY(value: number): number {
  return scale(value, leftTicks.value);
}

function barHeight(value: number): number {
  return MARGIN.top + plotHeight.value - leftY(value);
}

function centerX(i: number): number {
  return MARGIN.left + i * band.value + band.value / 2;
}

function barX(i: number): number {
  return centerX(i) - barWidth.value / 2;
}

function pillY(month: ReportingMonthType): number {
  const top = leftY(Math.max(barValue(month) ?? 0, projectedBar(month) ?? 0));
  return Math.max(2, top - PILL.height - PILL.gap);
}

function onTooltipShown(index: number, isShown: boolean): void {
  if (isShown) {
    activeIndex.value = index;
  } else if (activeIndex.value === index) {
    activeIndex.value = null;
  }
}

const lastYearPoints = computed<PointType[]>(() =>
  props.months.flatMap((month, i) => {
    const value = lastYearValue(month);
    return value === null ? [] : [{ x: centerX(i), y: leftY(value) }];
  }),
);

function money(value: number | null): string {
  if (value === null) {
    return "—";
  }
  return n(value, { style: "currency", currency: props.currencyCode, maximumFractionDigits: 0 });
}

function formatLeftTick(value: number): string {
  return n(value, { style: "currency", currency: props.currencyCode, notation: "compact", maximumFractionDigits: 1 });
}

function formatChange(value: number | null): string {
  if (value === null) {
    return "";
  }
  const percent = n(Math.abs(value) / 100, { style: "percent", minimumFractionDigits: 1, maximumFractionDigits: 1 });
  return `${value < 0 ? "↓" : "↑"} ${percent}`;
}

function monthLabel(month: ReportingMonthType): string {
  const label = d(new Date(month.year, month.month, 1), { month: isWide.value ? "short" : "narrow" });
  // The trailing dot marks a month that has not started, as in the design.
  return month.state === "upcoming" ? `${label} ·` : label;
}

/** "+23%", "−4.4%": the tooltip's signed change, unlike the arrow pills above the bars. */
function signedPercent(value: number): string {
  return n(value / 100, {
    style: "percent",
    signDisplay: "exceptZero",
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  });
}

function tooltipTitle(month: ReportingMonthType): string {
  return d(new Date(month.year, month.month, 1), { month: "short", year: "numeric" });
}

/** Label/value rows: the values line up in one right-aligned column so they read top to bottom. */
function tooltipRows(month: ReportingMonthType): ITooltipRow[] {
  const isCurrent = month.state === "current";
  const rows: ITooltipRow[] = [];

  if (props.kind === "aov") {
    rows.push({ label: t("sales_rep.reporting.tooltip.aov"), value: money(month.aov), isMoney: month.aov !== null });
  } else {
    rows.push({
      label: isCurrent ? t("sales_rep.reporting.tooltip.revenue_to_date") : t("sales_rep.reporting.tooltip.revenue"),
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
    const previous = d(new Date(month.year, month.month - 1, 1), { month: "short" });
    rows.push({
      label: t("sales_rep.reporting.tooltip.vs_month", { month: previous }),
      value: signedPercent(monthChange),
    });
  }

  const lastYearMonth = d(new Date(month.year - 1, month.month, 1), { month: "short", year: "numeric" });
  const yearChange = change(month);
  if (yearChange !== null) {
    rows.push({
      label: t("sales_rep.reporting.tooltip.vs_month", { month: lastYearMonth }),
      value: signedPercent(yearChange),
    });
  }
  rows.push({
    label:
      props.kind === "aov"
        ? t("sales_rep.reporting.tooltip.last_year_aov", { month: lastYearMonth })
        : t("sales_rep.reporting.tooltip.last_year_revenue", { month: lastYearMonth }),
    // A month without orders last year has no baseline: "—", not "$0", so it doesn't read as a real zero.
    value: yearChange === null ? "—" : money(lastYearValue(month)),
    isMoney: yearChange !== null,
  });
  return rows;
}

function tooltipAriaLabel(month: ReportingMonthType): string {
  return [tooltipTitle(month), ...tooltipRows(month).map((row) => `${row.label}: ${row.value}`)].join(". ");
}

// Each target starts at its column's top, so the tooltip (placed above it) sits on the bar, as in the design.
const hoverTargets = computed(() =>
  props.months.flatMap((month, index) => {
    if (month.state === "upcoming") {
      return [];
    }
    const baseline = MARGIN.top + plotHeight.value;
    const top = Math.min(baseline - MIN_HIT_HEIGHT, leftY(Math.max(barValue(month) ?? 0, projectedBar(month) ?? 0)));
    return [{ month, index, top, height: baseline - top }];
  }),
);

// YoY badges, centred above each column's tallest mark; hidden when the columns are too narrow to hold them.
const pills = computed(() => {
  if (!isWide.value) {
    return [];
  }
  return props.months.flatMap((month, i) => {
    const value = change(month);
    if (month.state === "upcoming" || value === null) {
      return [];
    }
    return [
      {
        key: `${month.year}-${month.month}`,
        x: centerX(i),
        y: pillY(month),
        isDown: value < 0,
        text: formatChange(value),
      },
    ];
  });
});
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

  &__last-year-switch {
    @apply ms-auto;
  }

  &__heading {
    @apply flex flex-wrap items-baseline gap-x-3 gap-y-1;
  }

  &__subtitle {
    @apply text-sm text-neutral-600;
  }

  &__plot {
    @apply relative min-h-[14.375rem] w-full;
  }

  &__svg {
    @apply block overflow-visible;
  }

  &__grid line {
    @apply stroke-neutral-200;
  }

  &__tick {
    @apply fill-neutral-600 text-xs;
  }

  &__axis-title {
    @apply fill-neutral-800 text-xs font-bold;
  }

  // One fill, read once: the kind and the month pick the base, hovering a column only darkens it.
  &__bar {
    --bar-base: var(--color-primary-200);
    --bar-shade: 0%;

    fill: color-mix(in srgb, var(--color-additional-950) var(--bar-shade), var(--bar-base));

    &--aov {
      --bar-base: var(--color-neutral-300);
    }

    &--current {
      --bar-base: var(--color-primary-500);
    }

    &--active {
      --bar-shade: 10%;
    }
  }

  // The design draws the month-end outline as strong as the bar it extends.
  &__projection {
    @apply fill-transparent stroke-primary-500;

    stroke-width: 1.5;
    stroke-dasharray: 3 2;
  }

  &__upcoming {
    @apply fill-transparent stroke-neutral-300;

    stroke-dasharray: 4 3;
  }

  &__upcoming-label {
    @apply fill-neutral-500 text-xs;

    &--small {
      @apply text-xxs;
    }
  }

  &__pill {
    @apply pointer-events-none absolute -translate-x-1/2;
  }

  &__month {
    @apply fill-neutral-800 text-xs font-bold;

    &--upcoming {
      @apply fill-neutral-400;
    }
  }

  &__last-year-line {
    @apply fill-transparent stroke-accent-500;

    stroke-width: 2;
  }

  &__last-year-dot {
    @apply fill-accent-500 stroke-additional-50;

    stroke-width: 1.5;
  }

  &__hit {
    @apply absolute;
  }

  &__hit-area {
    @apply block w-full cursor-default;
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

  &__legend {
    @apply flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-neutral-700;
  }

  &__legend-item {
    @apply flex items-center gap-2;
  }

  &__swatch {
    @apply inline-block size-3.5 bg-primary-200;

    &--aov {
      @apply bg-neutral-300;
    }

    &--current {
      @apply bg-primary-500;
    }

    &--projection {
      @apply border border-dashed border-primary-500 bg-transparent;
    }
  }

  &__line {
    @apply inline-block h-0.5 w-5;

    &--last-year {
      @apply bg-accent-500;
    }
  }

  &__note {
    @apply flex items-center gap-2 border-t border-neutral-200 pt-3 text-xs text-neutral-600;
  }
}
</style>
