import { ref } from "vue";
import { useAsync, useApiClient } from "@vc-shell/framework";
import {
  SalesRepAnalyticsDiagnosticsClient,
  AnalyticsDiagnosticsCheck,
} from "../../../../api_client/virtocommerce.salesrep";

export default () => {
  const { getApiClient } = useApiClient(SalesRepAnalyticsDiagnosticsClient);

  const checks = ref<AnalyticsDiagnosticsCheck[]>([]);
  // Distinguishes "not run yet" from "ran and returned nothing" for the empty state.
  const hasRun = ref(false);

  const { loading: runningDiagnostics, action: runDiagnostics } = useAsync<{
    storeId?: string;
    includeLiveData: boolean;
  }>(async (args) => {
    const apiClient = await getApiClient();
    const apiResult = await apiClient.runAnalyticsDiagnostics(args?.storeId, args?.includeLiveData);
    checks.value = apiResult?.checks ?? [];
    hasRun.value = true;
  });

  return {
    checks,
    hasRun,
    runDiagnostics,
    runningDiagnostics,
  };
};
