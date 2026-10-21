<template>
  <div class="coupon-list">
    <!-- VcEmptyView has no live semantics, so a failure or an empty store would go unannounced. Mounted with the list
         and only its text swapped: a live region that appears together with its text is not reliably announced. -->
    <p class="coupon-list__status" role="status" aria-live="polite" aria-atomic="true">{{ statusText }}</p>

    <!-- A failure replaces the rows: apollo keeps the previous rows on a failed refetch. -->
    <VcEmptyView v-if="failed && !loading" :text="t('sales_rep.coupons.load_failed')" variant="error" />

    <VcEmptyView v-else-if="!coupons.length && !loading" :text="t('sales_rep.coupons.empty')" icon="tag" />

    <ul v-else-if="loading && !coupons.length" class="coupon-list__items" aria-hidden="true">
      <li v-for="index in skeletonRows" :key="index">
        <CouponItemSkeleton :compact="compact" />
      </li>
    </ul>

    <!-- The host's own card (facade export), so a coupon looks the same here as on the Promotion coupons page. -->
    <ul v-else class="coupon-list__items">
      <li v-for="coupon in coupons" :key="coupon.id">
        <CouponItem :coupon="coupon" :compact="compact">
          <template #description-append>
            <span class="coupon-list__uses">{{ couponUsesLabel(coupon, t) }}</span>
          </template>
        </CouponItem>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { CouponItem, CouponItemSkeleton } from "@vc-frontend/core";
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { couponUsesLabel } from "../coupons";
import type { SalesRepCouponType } from "../composables/useSalesRepCoupons";

interface IProps {
  coupons: SalesRepCouponType[];
  loading: boolean;
  failed: boolean;
  // Placeholder cards on the first load, matching the rows about to arrive.
  skeletonRows: number;
  // The host card's narrow layout, for a widget column.
  compact?: boolean;
}

const props = withDefaults(defineProps<IProps>(), { compact: false });

const { t } = useI18n();

const statusText = computed(() => {
  if (props.loading) {
    return "";
  }

  if (props.failed) {
    return t("sales_rep.coupons.load_failed");
  }

  return props.coupons.length ? "" : t("sales_rep.coupons.empty");
});
</script>

<style lang="scss">
.coupon-list {
  &__status {
    @apply sr-only;
  }

  &__items {
    @apply flex flex-col gap-3;
  }

  &__uses {
    @apply mt-1 text-xs text-neutral-500;
  }
}
</style>
