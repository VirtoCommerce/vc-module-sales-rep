<template>
  <div class="sales-rep-dashboard">
    <VcTypography class="sales-rep-dashboard__title" tag="h1">
      {{ t("sales_rep.hub.dashboard.page.title") }}
    </VcTypography>

    <LayoutSurface :layout="layout" :cards="cards" />
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { LayoutSurface, useLayout } from "@vc-frontend/core";
import { useSalesRepDashboardWidgets } from "../composables/useSalesRepDashboardWidgets";
import { DASHBOARD_LAYOUT_SCOPE } from "../constants";

const { t } = useI18n();

// The page owns the layout: the host's surface renders it, and the statistics queries are shaped from the
// same object, so the stat row and the queries behind it can never disagree on what is shown.
const layout = useLayout(DASHBOARD_LAYOUT_SCOPE);
const { cards } = useSalesRepDashboardWidgets(layout);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-dashboard {
  @apply flex flex-col gap-5;

  &__title {
    @apply [word-break:break-word];
  }
}
</style>
