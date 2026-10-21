<template>
  <LayoutWidget :title="title" size="md" class="sales-rep-coupons">
    <template v-if="organizationId" #append>
      <VcLink
        :to="{ name: CUSTOMER_COUPONS_ROUTE_NAME, params: { organizationId } }"
        class="sales-rep-coupons__all-link"
      >
        {{ t("sales_rep.coupons.view_all") }}

        <VcIcon name="arrow-right" size="xs" />
      </VcLink>
    </template>

    <!-- VcEmptyView has no live semantics, so a failure or an empty store would go unannounced. Mounted with the widget
         and only its text swapped: a live region that appears together with its text is not reliably announced. -->
    <p class="sales-rep-coupons__status" role="status" aria-live="polite" aria-atomic="true">{{ statusText }}</p>

    <!-- A failure replaces the rows: apollo keeps the previous rows on a failed refetch. -->
    <VcEmptyView v-if="failed && !loading" :text="t('sales_rep.coupons.load_failed')" variant="error" />

    <VcEmptyView v-else-if="!coupons.length && !loading" :text="t('sales_rep.coupons.empty')" icon="tag" />

    <ul v-else-if="loading && !coupons.length" class="sales-rep-coupons__list" aria-hidden="true">
      <li v-for="index in rowLimit" :key="index">
        <CouponItemSkeleton compact />
      </li>
    </ul>

    <ul v-else class="sales-rep-coupons__list">
      <!-- The host's own card (facade export), so a coupon looks the same here as on the Promotion coupons page. -->
      <li v-for="coupon in coupons" :key="coupon.id">
        <CouponItem :coupon="coupon" compact>
          <template #description-append>
            <span class="sales-rep-coupons__uses">{{ couponUsesLabel(coupon, t) }}</span>
          </template>
        </CouponItem>
      </li>
    </ul>
  </LayoutWidget>
</template>

<script setup lang="ts">
import { CouponItem, CouponItemSkeleton } from "@vc-frontend/core";
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useBlockChrome } from "../composables/useBlockChrome";
import { useSalesRepCoupons } from "../composables/useSalesRepCoupons";
import { COUPONS_DEFAULT_ROWS, CUSTOMER_COUPONS_ROUTE_NAME } from "../constants";
import { couponUsesLabel } from "../coupons";
import LayoutWidget from "./layout-widget.vue";

interface IProps {
  // Omit inside a layout; LayoutWidget then falls back to the block's titleKey.
  title?: string;
  // The profile's customer, for the "All promotion coupons" link; the list itself is store-wide.
  organizationId?: string;
}

withDefaults(defineProps<IProps>(), { title: undefined, organizationId: undefined });

const { t } = useI18n();
const chrome = useBlockChrome();
// The saved cap, not the draft one, so an unsaved row count does not refire the query.
const rowLimit = computed(() => chrome?.savedSettings.value.maxRows ?? COUPONS_DEFAULT_ROWS);

const { coupons, loading, error } = useSalesRepCoupons({ first: () => rowLimit.value });

const failed = computed(() => Boolean(error.value));

const statusText = computed(() => {
  if (loading.value) {
    return "";
  }

  if (failed.value) {
    return t("sales_rep.coupons.load_failed");
  }

  return coupons.value.length ? "" : t("sales_rep.coupons.empty");
});
</script>

<style lang="scss">
.sales-rep-coupons {
  &__status {
    @apply sr-only;
  }

  &__all-link {
    @apply inline-flex items-center gap-1 whitespace-nowrap text-sm font-medium text-[--link-color] hover:text-[--link-hover-color];
  }

  &__list {
    @apply flex flex-col gap-3;
  }

  &__uses {
    @apply mt-1 text-xs text-neutral-500;
  }
}
</style>
