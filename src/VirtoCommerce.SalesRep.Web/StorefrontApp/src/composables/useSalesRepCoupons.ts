import { computed, toValue } from "vue";
import { globals, Logger } from "@vc-frontend/core";
import { SalesRepCouponsDocument } from "../api/graphql/types";
import { COUPONS_DEFAULT_ROWS, HUB_FETCH_POLICY } from "../constants";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";
import type { SalesRepCouponFieldsFragment } from "../api/graphql/types";
import type { Ref } from "vue";

export type SalesRepCouponType = SalesRepCouponFieldsFragment;

type UseSalesRepCouponsOptionsType = {
  first?: number | Ref<number | undefined> | (() => number | undefined);
  // Offset-as-cursor, like the other paged lists; omit for the first page.
  after?: string | Ref<string | undefined> | (() => string | undefined);
};

/**
 * The store's public promotion coupons (VCST-6158). Not per customer: every customer sees the same list, so the
 * customer profile widget and the full coupons page read one query. Empty while vc-module-marketing is absent.
 */
export function useSalesRepCoupons(options: UseSalesRepCouponsOptionsType = {}) {
  const variables = computed(() => ({
    storeId: globals.storeId,
    // Localize the promotion's label, name and description.
    cultureName: globals.cultureName,
    first: toValue(options.first) ?? COUPONS_DEFAULT_ROWS,
    after: toValue(options.after),
  }));

  const { result, loading, error, onError } = useSalesRepHubQuery(SalesRepCouponsDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    // Holds the current rows while the next page or a new row cap loads, so the total never reads 0 in between.
    keepPreviousResult: true,
  });

  onError((err) => {
    // No toast; the widget's empty view names the failure instead.
    Logger.error("[sales-rep] salesRepCoupons failed:", err);
  });

  const coupons = computed<SalesRepCouponType[]>(() =>
    (result.value?.salesRepCoupons?.items ?? []).filter((item): item is SalesRepCouponType => Boolean(item)),
  );
  const totalCount = computed(() => result.value?.salesRepCoupons?.totalCount ?? 0);
  const failed = computed(() => Boolean(error.value));

  return { coupons, totalCount, loading, failed };
}
