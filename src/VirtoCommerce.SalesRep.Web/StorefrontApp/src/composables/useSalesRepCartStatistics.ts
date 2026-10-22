import { computed, toValue } from "vue";
import { buildStatisticsWindows, globals, Logger } from "@vc-frontend/core";
import { SalesRepCustomerCartStatisticsDocument } from "../api/graphql/types";
import { ACTIVE_CARTS_FILTER, HUB_FETCH_POLICY } from "../constants";
import { needsCartStatistics } from "../layout/stat-data-needs";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";
import type { HubStatisticsOptionsType } from "../types/widgets";

// Two aliased slices from one query: activeCarts backs the "Active carts" card, itemsThisWeek backs its delta.
// That one card is the whole reason for the round trip, so with it hidden the query does not run.
export function useSalesRepCartStatistics(options: HubStatisticsOptionsType) {
  const { needs, ready } = options.needs;

  const variables = computed(() => {
    const windows = buildStatisticsWindows();
    return {
      organizationId: toValue(options.organizationId),
      storeId: globals.storeId,
      currencyCode: globals.currencyCode,
      cartFilter: ACTIVE_CARTS_FILTER,
      weekFrom: windows.weekFrom,
      weekTo: windows.weekTo,
    };
  });

  const enabled = computed(() => ready.value && needsCartStatistics(needs.value));

  const { result, loading, error, onError } = useSalesRepHubQuery(SalesRepCustomerCartStatisticsDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    enabled,
  });

  onError((err) => {
    Logger.error("[sales-rep] salesRepCustomerCartStatistics failed:", err);
  });

  const statistics = computed(() => result.value?.salesRepCustomerCartStatistics);

  return {
    statistics,
    loading: computed(() => !ready.value || loading.value),
    error,
  };
}
