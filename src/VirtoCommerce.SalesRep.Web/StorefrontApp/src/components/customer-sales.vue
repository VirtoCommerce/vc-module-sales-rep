<template>
  <LayoutWidget :title="title" size="md" class="customer-sales">
    <template #append>
      <VcLink :to="reportingRoute" class="customer-sales__reporting-link">
        {{ t("sales_rep.customer_profile.sales.open_in_reporting") }}

        <VcIcon name="arrow-right" size="xs" />
      </VcLink>
    </template>

    <ReportingPanel v-model:period="period" :organization-id="organizationId" :with-table="false" compact />
  </LayoutWidget>
</template>

<script setup lang="ts">
import { useLocalStorage } from "@vueuse/core";
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { CUSTOMER_SALES_PERIOD_STORAGE_KEY, REPORTING_ROUTE_NAME } from "../constants";
import { isReportingPeriod, REPORTING_DEFAULT_PERIOD } from "../reporting";
import LayoutWidget from "./layout-widget.vue";
import ReportingPanel from "./reporting-panel.vue";
import type { ReportingPeriodType } from "../reporting";

interface IProps {
  // Widget heading — the caller decides the wording.
  title: string;
  // The customer whose profile this is.
  organizationId: string;
}

const props = defineProps<IProps>();

const { t } = useI18n();

// Per browser and apart from the Reporting page's URL: a rep going through customers keeps one period.
const storedPeriod = useLocalStorage<string>(CUSTOMER_SALES_PERIOD_STORAGE_KEY, REPORTING_DEFAULT_PERIOD);

// Guarded on read: storage can hold anything, including a period a later release dropped.
const period = computed<ReportingPeriodType>({
  get: () => (isReportingPeriod(storedPeriod.value) ? storedPeriod.value : REPORTING_DEFAULT_PERIOD),
  set: (value) => {
    storedPeriod.value = value;
  },
});

// The full report for this customer and period, with the table this block leaves out.
const reportingRoute = computed(() => ({
  name: REPORTING_ROUTE_NAME,
  query: { customer: props.organizationId, period: period.value },
}));
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-sales {
  &__reporting-link {
    @apply inline-flex items-center gap-1 whitespace-nowrap text-sm font-medium text-[--link-color] hover:text-[--link-hover-color];
  }
}
</style>
