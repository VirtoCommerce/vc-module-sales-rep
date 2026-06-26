import { ref } from "vue";
import { useAsync, useApiClient } from "@vc-shell/framework";
import { StoreModuleClient, Store, StoreSearchCriteria } from "../../../../api_client/virtocommerce.store";

export default () => {
  const { getApiClient } = useApiClient(StoreModuleClient);

  const stores = ref<Store[]>([]);

  const { loading: loadingStores, action: loadStores } = useAsync(async () => {
    const apiClient = await getApiClient();
    const apiResult = await apiClient.searchStores({ take: 999 } as StoreSearchCriteria);
    if (apiResult?.results) {
      stores.value = apiResult.results;
    }
  });

  return {
    stores,
    loadStores,
    loadingStores,
  };
};
