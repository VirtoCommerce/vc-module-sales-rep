<template>
  <div class="customer-coupons">
    <VcBreadcrumbs :items="breadcrumbs" />

    <VcTypography tag="h1" class="customer-coupons__title">
      {{ heading }}
    </VcTypography>

    <!-- The list announces its own outcomes; this one is the page's. Mounted with the page and only its text swapped:
         a live region that appears together with its text is not reliably announced. -->
    <p class="customer-coupons__status" role="status" aria-live="polite" aria-atomic="true">
      {{ notFound ? t("sales_rep.customer_profile.not_found") : "" }}
    </p>

    <VcEmptyView v-if="notFound" :text="t('sales_rep.customer_profile.not_found')" icon="outline-404">
      <template #button>
        <VcButton :to="{ name: CUSTOMER_PROFILE_ROUTE_NAME, params: { organizationId } }" prepend-icon="arrow-left">
          {{ t("sales_rep.customer_orders.back_to_customer") }}
        </VcButton>
      </template>
    </VcEmptyView>

    <template v-else>
      <!-- Full width here, so the host card's regular (not compact) layout, as on the buyer's own coupons page. -->
      <CouponList :coupons="coupons" :loading="loading" :failed="failed" :skeleton-rows="COUPONS_PAGE_SIZE" />

      <VcPagination v-if="pages > 1" v-model:page="page" :pages="pages" @update:page="changePage" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { ROUTES, useBreadcrumbs, usePageHead } from "@vc-frontend/core";
import CouponList from "../components/coupon-list.vue";
import { useSalesRepCustomerCoupons } from "../composables/useSalesRepCustomerCoupons";
import {
  COUPONS_PAGE_SIZE,
  CUSTOMER_PROFILE_ROUTE_NAME,
  DASHBOARD_ROUTE_NAME,
  MY_CUSTOMERS_ROUTE_NAME,
} from "../constants";

interface IProps {
  organizationId: string;
}

const props = defineProps<IProps>();

const { t } = useI18n();

const { customer, notFound, coupons, loading, failed, page, pages } = useSalesRepCustomerCoupons(
  () => props.organizationId,
);

const customerName = computed(() => customer.value?.organizationName ?? "");
const heading = computed(() =>
  customerName.value
    ? t("sales_rep.coupons.page.title", { customer: customerName.value })
    : t("sales_rep.coupons.page.title_fallback"),
);

function changePage(): void {
  window.scroll({ top: 0, behavior: "smooth" });
}

usePageHead({ title: heading });

const breadcrumbs = useBreadcrumbs(() => [
  { title: t("common.links.account"), route: { name: ROUTES.ACCOUNT.NAME } },
  { title: t("sales_rep.hub.title"), route: { name: DASHBOARD_ROUTE_NAME } },
  { title: t("sales_rep.my_customers.page.title"), route: { name: MY_CUSTOMERS_ROUTE_NAME } },
  {
    title: customerName.value,
    route: { name: CUSTOMER_PROFILE_ROUTE_NAME, params: { organizationId: props.organizationId } },
  },
  { title: t("sales_rep.coupons.breadcrumb") },
]);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-coupons {
  @apply flex flex-col gap-5;

  &__title {
    @apply [word-break:break-word];
  }

  &__status {
    @apply sr-only;
  }
}
</style>
