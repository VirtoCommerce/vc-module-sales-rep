<template>
  <details class="reporting-table" data-test-id="sales-rep-reporting-table-section">
    <summary class="reporting-table__toggle">{{ t("sales_rep.reporting.table.toggle") }}</summary>

    <VcTable
      class="reporting-table__table"
      :items="rows"
      :description="t('sales_rep.reporting.table.description')"
      mobile-breakpoint="md"
      bordered
    >
      <template #mobile-item="{ item }">
        <div class="reporting-table__mobile-item">
          <strong>{{ item.month }}</strong>

          <dl class="reporting-table__mobile-fields">
            <template v-for="field in MOBILE_FIELDS" :key="field.id">
              <dt class="reporting-table__mobile-label">{{ t(field.title) }}</dt>

              <dd class="reporting-table__mobile-value">{{ item[field.id] }}</dd>
            </template>
          </dl>
        </div>
      </template>

      <VcTableColumn id="month" v-slot="{ item }" :title="t('sales_rep.reporting.table.month')">
        <strong>{{ item.month }}</strong>
      </VcTableColumn>

      <VcTableColumn id="revenue" v-slot="{ item }" :title="t('sales_rep.reporting.table.sales')" align="right">
        <VcPriceDisplay :value="{ formattedAmount: item.revenue }" />
      </VcTableColumn>

      <VcTableColumn id="orders" v-slot="{ item }" :title="t('sales_rep.reporting.table.orders')" align="right">
        {{ item.orders }}
      </VcTableColumn>

      <VcTableColumn id="aov" v-slot="{ item }" :title="t('sales_rep.reporting.table.aov')" align="right">
        <VcPriceDisplay :value="{ formattedAmount: item.aov }" />
      </VcTableColumn>

      <VcTableColumn id="lastYear" v-slot="{ item }" :title="t('sales_rep.reporting.table.last_year')" align="right">
        <VcPriceDisplay :value="{ formattedAmount: item.lastYear }" />
      </VcTableColumn>

      <VcTableColumn id="change" v-slot="{ item }" :title="t('sales_rep.reporting.table.change')" align="right">
        {{ item.change }}
      </VcTableColumn>
    </VcTable>
  </details>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useReportingFormatters } from "../composables/useReportingFormatters";
import { lastYearBaseline, REPORTING_NO_VALUE } from "../reporting";
import type { ReportingMonthType } from "../reporting";

interface IProps {
  months: ReportingMonthType[];
  currencyCode: string;
}

type RowType = {
  id: string;
  month: string;
  revenue: string;
  orders: string;
  aov: string;
  lastYear: string;
  change: string;
};

// The phone layout lists every column under its month, each with its own label.
const MOBILE_FIELDS: { id: Exclude<keyof RowType, "id" | "month">; title: string }[] = [
  { id: "revenue", title: "sales_rep.reporting.table.sales" },
  { id: "orders", title: "sales_rep.reporting.table.orders" },
  { id: "aov", title: "sales_rep.reporting.table.aov" },
  { id: "lastYear", title: "sales_rep.reporting.table.last_year" },
  { id: "change", title: "sales_rep.reporting.table.change" },
];

const props = defineProps<IProps>();

const { t, n } = useI18n();
const formatters = useReportingFormatters();

const rows = computed<RowType[]>(() =>
  props.months
    .filter((month) => month.state !== "upcoming")
    .map((month) => ({
      id: `${month.year}-${month.month}`,
      month: formatters.monthYear(month.year, month.month),
      revenue: formatters.money(month.revenue, props.currencyCode),
      orders: n(month.orders ?? 0),
      aov: formatters.money(month.aov, props.currencyCode),
      lastYear: formatters.money(lastYearBaseline(month, "sales"), props.currencyCode),
      change: month.revenueChange === null ? REPORTING_NO_VALUE : formatters.signedChange(month.revenueChange),
    })),
);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.reporting-table {
  @apply flex flex-col gap-3;

  &__toggle {
    @apply w-fit cursor-pointer text-sm font-bold text-[--link-color];

    &:hover {
      @apply text-[--link-hover-color];
    }
  }

  &__mobile-item {
    @apply flex flex-col gap-1 text-sm;
  }

  &__mobile-fields {
    @apply grid grid-cols-[1fr_auto] gap-x-4 gap-y-0.5;
  }

  &__mobile-label {
    @apply text-neutral-600;
  }

  &__mobile-value {
    @apply text-end font-bold tabular-nums;
  }
}
</style>
