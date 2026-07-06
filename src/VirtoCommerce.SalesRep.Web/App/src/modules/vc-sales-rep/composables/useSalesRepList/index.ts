import { ref } from "vue";
import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import {
  SalesRepClient,
  SalesRepListItem,
  SalesRepSearchCriteria,
} from "../../../../api_client/virtocommerce.salesrep";

export default () => {
  const pageSize = 20;

  const { getApiClient } = useApiClient(SalesRepClient);

  const searchQuery = ref<SalesRepSearchCriteria>({} as SalesRepSearchCriteria);
  const salesReps = ref<SalesRepListItem[]>([]);
  const salesRepsCount = ref(0);
  const pageIndex = ref(1);

  const { loading: searching, action: searchSalesReps } = useAsync(async () => {
    const apiClient = await getApiClient();
    const apiResult = await apiClient.search({
      ...(searchQuery.value ?? {}),
      take: pageSize,
      skip: pageSize * (pageIndex.value - 1),
    } as SalesRepSearchCriteria);

    if (apiResult) {
      salesReps.value = apiResult.results ?? [];
      salesRepsCount.value = apiResult.totalCount ?? 0;
    }
  });

  const { loading: deleting, action: deleteSalesReps } = useAsync<{ ids: string[] }>(async (args?: { ids: string[] }) => {
    if (args) {
      const apiClient = await getApiClient();
      await apiClient.delete(args.ids);
    }
  });

  return {
    salesReps,
    salesRepsCount,
    pageSize,
    pageIndex,
    searchQuery,
    searchSalesReps,
    deleteSalesReps,
    loadingSalesReps: useLoading(searching, deleting),
  };
};
