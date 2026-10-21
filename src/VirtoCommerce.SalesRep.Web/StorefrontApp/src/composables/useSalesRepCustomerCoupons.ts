import { computed, ref, watch } from "vue";
import { COUPONS_PAGE_SIZE } from "../constants";
import { useSalesRepCoupons } from "./useSalesRepCoupons";
import { useSalesRepCustomer } from "./useSalesRepCustomer";
import type { MaybeRefOrGetter } from "vue";

/**
 * The all-coupons page under a customer's profile (VCST-6158): the store-wide coupon list, paged, plus the customer
 * that frames the page (heading, breadcrumbs, and whether the rep serves them at all).
 */
export function useSalesRepCustomerCoupons(organizationId: MaybeRefOrGetter<string>) {
  const { customer, notFound } = useSalesRepCustomer(organizationId);

  const page = ref(1);
  const { coupons, totalCount, loading, error } = useSalesRepCoupons({
    first: COUPONS_PAGE_SIZE,
    after: () => String((page.value - 1) * COUPONS_PAGE_SIZE),
  });

  const failed = computed(() => Boolean(error.value));
  const pages = computed(() => Math.max(1, Math.ceil(totalCount.value / COUPONS_PAGE_SIZE)));

  // A coupon can expire or be used up between pages; never strand the rep past the last one. Only on a settled
  // read: while the next page loads apollo has no result for its variables, so the total reads 0 and would snap
  // back to 1.
  watch([pages, loading], ([total, isLoading]) => {
    if (!isLoading && page.value > total) {
      page.value = total;
    }
  });

  return { customer, notFound, coupons, loading, failed, page, pages };
}
