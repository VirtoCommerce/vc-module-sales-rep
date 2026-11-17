import { computed, toValue } from "vue";
import { buildStatisticsWindows, globals, Logger } from "@vc-frontend/core";
import { SalesRepCustomerOrderStatisticsDocument } from "../api/graphql/types";
import { HUB_FETCH_POLICY, NEW_ORDERS_FILTER } from "../constants";
import { needsOrderStatistics, orderStatisticsFlags } from "../layout/stat-data-needs";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";
import type { HubStatisticsOptionsType } from "../types/widgets";

// One composable owns the salesRepCustomerOrderStatistics op; all period/comparison slices are aliased
// into a single query shared by the dashboard and customer mappers. Which of them it actually asks for
// comes from the cards the rep can see, so the two surfaces no longer pay for each other's slices
// (VCST-5647) — and with none of them needed, the query does not run.
export function useSalesRepOrderStatistics(options: HubStatisticsOptionsType) {
  const { needs, ready } = options.needs;

  const variables = computed(() => ({
    organizationId: toValue(options.organizationId),
    storeId: globals.storeId,
    currencyCode: globals.currencyCode,
    cultureName: globals.cultureName,
    newOrdersFilter: NEW_ORDERS_FILTER,
    ...buildStatisticsWindows(),
    ...orderStatisticsFlags(needs.value),
  }));

  const enabled = computed(() => ready.value && needsOrderStatistics(needs.value));

  const { result, loading, error, onError } = useSalesRepHubQuery(SalesRepCustomerOrderStatisticsDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    enabled,
    keepPreviousResult: true,
  });

  onError((err) => {
    // No toast; `error` is surfaced on the cards themselves (VCST-5586).
    Logger.error("[sales-rep] salesRepCustomerOrderStatistics failed:", err);
  });

  const statistics = computed(() => result.value?.salesRepCustomerOrderStatistics);

  return {
    statistics,
    // Waiting on the layout is still waiting: the cards must not read as zeros in the meantime.
    loading: computed(() => !ready.value || loading.value),
    error,
  };
}
