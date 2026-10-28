import { computed } from "vue";
import { Logger } from "@vc-frontend/core";
import { SalesRepOverdueTaskCountDocument } from "../api/graphql/types";
import { HUB_FETCH_POLICY } from "../constants";
import { startOfLocalDayIso } from "../tasks";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";

/** The dashboard widget's overdue notice: how many of the rep's tasks are overdue, without the rows. */
export function useSalesRepOverdueTaskCount() {
  const today = startOfLocalDayIso();

  const { result, onError } = useSalesRepHubQuery(
    SalesRepOverdueTaskCountDocument,
    computed(() => ({ today })),
    { fetchPolicy: HUB_FETCH_POLICY },
  );

  onError((err) => {
    Logger.error("[sales-rep] salesRepOverdueTaskCount failed:", err);
  });

  const overdueCount = computed(() => result.value?.overdue?.totalCount ?? 0);

  return { overdueCount };
}
