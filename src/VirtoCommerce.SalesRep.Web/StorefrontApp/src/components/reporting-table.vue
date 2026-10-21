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

          <span>{{ item.revenue }} · {{ item.orders }} · {{ item.aov }}</span>
        </div>
      </template>

      <VcTableColumn id="month" v-slot="{ item }" :title="t('sales_rep.reporting.table.month')" strong>
        {{ item.month }}
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
import type { ReportingMonthType } from "../reporting";

interface IProps {
  months: ReportingMonthType[];
  currencyCode: string;
}

const props = defineProps<IProps>();

const { t, n, d } = useI18n();

function money(value: number | null): string {
  return value === null ? "—" : n(value, { style: "currency", currency: props.currencyCode, maximumFractionDigits: 0 });
}

const rows = computed(() =>
  props.months
    .filter((month) => month.state !== "upcoming")
    .map((month) => ({
      id: `${month.year}-${month.month}`,
      month: d(new Date(month.year, month.month, 1), { month: "short", year: "numeric" }),
      revenue: money(month.revenue),
      orders: n(month.orders ?? 0),
      aov: money(month.aov),
      lastYear: money(month.lastYearRevenue),
      change:
        month.revenueChange === null
          ? "—"
          : n(month.revenueChange / 100, {
              style: "percent",
              signDisplay: "exceptZero",
              minimumFractionDigits: 1,
              maximumFractionDigits: 1,
            }),
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
    @apply flex flex-col gap-0.5 text-sm;
  }
}
</style>
