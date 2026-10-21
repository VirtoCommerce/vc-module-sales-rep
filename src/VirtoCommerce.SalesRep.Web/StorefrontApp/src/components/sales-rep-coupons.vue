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

    <CouponList :coupons="coupons" :loading="loading" :failed="failed" :skeleton-rows="rowLimit" compact />
  </LayoutWidget>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useBlockChrome } from "../composables/useBlockChrome";
import { useSalesRepCoupons } from "../composables/useSalesRepCoupons";
import { COUPONS_DEFAULT_ROWS, CUSTOMER_COUPONS_ROUTE_NAME } from "../constants";
import CouponList from "./coupon-list.vue";
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

const { coupons, loading, failed } = useSalesRepCoupons({ first: () => rowLimit.value });
</script>

<style lang="scss">
.sales-rep-coupons {
  &__all-link {
    @apply inline-flex items-center gap-1 whitespace-nowrap text-sm font-medium text-[--link-color] hover:text-[--link-hover-color];
  }
}
</style>
