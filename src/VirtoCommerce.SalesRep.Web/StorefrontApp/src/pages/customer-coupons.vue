<template>
  <div class="customer-coupons">
    <VcBreadcrumbs :items="breadcrumbs" />

    <VcTypography tag="h1" class="customer-coupons__title">
      {{ heading }}
    </VcTypography>

    <!-- VcEmptyView has no live semantics, so these outcomes would go unannounced. Mounted with the page and only its
         text swapped: a live region that appears together with its text is not reliably announced. -->
    <p class="customer-coupons__status" role="status" aria-live="polite" aria-atomic="true">{{ statusText }}</p>

    <VcEmptyView v-if="notFound" :text="t('sales_rep.customer_profile.not_found')" icon="outline-404">
      <template #button>
        <VcButton :to="{ name: CUSTOMER_PROFILE_ROUTE_NAME, params: { organizationId } }" prepend-icon="arrow-left">
          {{ t("sales_rep.customer_orders.back_to_customer") }}
        </VcButton>
      </template>
    </VcEmptyView>

    <!-- A failure replaces the rows: apollo keeps the previous rows on a failed refetch. -->
    <VcEmptyView v-else-if="failed && !loading" :text="t('sales_rep.coupons.load_failed')" variant="error" />

    <VcEmptyView v-else-if="!coupons.length && !loading" :text="t('sales_rep.coupons.empty')" icon="tag" />

    <ul v-else-if="loading && !coupons.length" class="customer-coupons__list" aria-hidden="true">
      <li v-for="index in COUPONS_PAGE_SIZE" :key="index">
        <CouponItemSkeleton />
      </li>
    </ul>

    <template v-else>
      <!-- Full width here, so the host card's regular (not compact) layout, as on the buyer's own coupons page. -->
      <ul class="customer-coupons__list">
        <li v-for="coupon in coupons" :key="coupon.id">
          <CouponItem :coupon="coupon">
            <template #description-append>
              <span class="customer-coupons__uses">{{ couponUsesLabel(coupon, t) }}</span>
            </template>
          </CouponItem>
        </li>
      </ul>

      <VcPagination v-if="pages > 1" v-model:page="page" :pages="pages" @update:page="changePage" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { CouponItem, CouponItemSkeleton, useBreadcrumbs, usePageHead } from "@vc-frontend/core";
import { useSalesRepCustomerCoupons } from "../composables/useSalesRepCustomerCoupons";
import {
  COUPONS_PAGE_SIZE,
  CUSTOMER_PROFILE_ROUTE_NAME,
  DASHBOARD_ROUTE_NAME,
  MY_CUSTOMERS_ROUTE_NAME,
} from "../constants";
import { couponUsesLabel } from "../coupons";

interface IProps {
  organizationId: string;
}

const props = defineProps<IProps>();

const { t } = useI18n();

const { customer, notFound, coupons, loading, failed, page, pages } = useSalesRepCustomerCoupons(
  () => props.organizationId,
);

const statusText = computed(() => {
  if (notFound.value) {
    return t("sales_rep.customer_profile.not_found");
  }

  if (loading.value) {
    return "";
  }

  if (failed.value) {
    return t("sales_rep.coupons.load_failed");
  }

  return coupons.value.length ? "" : t("sales_rep.coupons.empty");
});

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
  { title: t("common.links.account"), route: { name: "Account" } },
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

  &__list {
    @apply flex flex-col gap-3;
  }

  &__uses {
    @apply mt-1 text-xs text-neutral-500;
  }
}
</style>
