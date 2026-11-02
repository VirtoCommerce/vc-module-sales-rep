<template>
  <div class="sales-rep-reporting">
    <VcTypography class="sales-rep-reporting__title" tag="h1">
      {{ t("sales_rep.reporting.title") }}
    </VcTypography>

    <p class="sales-rep-reporting__subtitle">{{ t("sales_rep.reporting.subtitle") }}</p>

    <VcWidget class="sales-rep-reporting__card">
      <template #header>
        <div class="sales-rep-reporting__card-header">
          <VcTypography tag="h2" variant="h6" class="sales-rep-reporting__card-title">
            {{ t("sales_rep.reporting.card_title") }}
          </VcTypography>

          <VcLink class="sales-rep-reporting__all-orders" :to="{ name: ALL_CUSTOMER_ORDERS_ROUTE_NAME }">
            {{ t("sales_rep.reporting.all_orders") }}

            <VcIcon name="arrow-right" size="xs" />
          </VcLink>
        </div>
      </template>

      <ReportingPanel v-model:period="period" :organization-id="customerId">
        <template #actions>
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
            @change="customerId = $event ?? REPORTING_ALL_CUSTOMERS"
          />
        </template>
      </ReportingPanel>
    </VcWidget>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { usePageHead } from "@vc-frontend/core";
import ReportingPanel from "../components/reporting-panel.vue";
import { useReportingQueryParams } from "../composables/useReportingQueryParams";
import { useSalesRepCustomerOptions } from "../composables/useSalesRepCustomerOptions";
import { ALL_CUSTOMER_ORDERS_ROUTE_NAME } from "../constants";
import { REPORTING_ALL_CUSTOMERS } from "../reporting";

const { t, n } = useI18n();

const customerOptions = useSalesRepCustomerOptions();
const {
  options: customers,
  totalCount: customersCount,
  loading: customersLoading,
  failed: customersFailed,
} = customerOptions;

const { period, customerId } = useReportingQueryParams(customerOptions);

// No count until the list has loaded: "(0)" over a report with figures would read as "no customers".
const allCustomersLabel = computed(() =>
  customersLoading.value || customersFailed.value
    ? t("sales_rep.reporting.all_customers_plain")
    : t("sales_rep.reporting.all_customers", { count: n(customersCount.value) }),
);

const customerItems = computed(() => [
  { organizationId: REPORTING_ALL_CUSTOMERS, organizationName: allCustomersLabel.value },
  ...customers.value,
]);

usePageHead({
  title: computed(() => t("sales_rep.reporting.title")),
});
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

  &__all-orders {
    @apply inline-flex items-center gap-1 text-sm font-bold;
  }

  &__customer {
    @apply min-w-0 flex-1;

    @media (width >= theme("screens.md")) {
      @apply w-64 flex-none;
    }
  }
}
</style>
