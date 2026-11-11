import { computed, toValue } from "vue";
import { buildStatisticsWindows, globals, Logger } from "@vc-frontend/core";
import { SalesRepCustomerCountsDocument } from "../api/graphql/types";
import { HUB_FETCH_POLICY } from "../constants";
import { needsCustomerCounts } from "../layout/stat-data-needs";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";
import type { HubStatisticsOptionsType } from "../types/widgets";

// Owns the salesRepCustomerCounts op: served/ordered/new customer counts, backing the "My customers" card.
// That one card is the whole reason for the round trip, so with it hidden the query does not run.
export function useSalesRepCustomerCounts(options: HubStatisticsOptionsType) {
  const { needs, ready } = options.needs;

  const variables = computed(() => {
    const windows = buildStatisticsWindows();
    return {
      organizationId: toValue(options.organizationId),
      storeId: globals.storeId,
      mtdFrom: windows.mtdFrom,
      mtdTo: windows.mtdTo,
    };
  });

  const enabled = computed(() => ready.value && needsCustomerCounts(needs.value));

  const { result, loading, error, onError } = useSalesRepHubQuery(SalesRepCustomerCountsDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    enabled,
  });

  onError((err) => {
    Logger.error("[sales-rep] salesRepCustomerCounts failed:", err);
  });

  const counts = computed(() => result.value?.salesRepCustomerCounts);

  return {
    counts,
    loading: computed(() => !ready.value || loading.value),
    error,
  };
}
