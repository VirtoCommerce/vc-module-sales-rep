import { ref } from "vue";
import { useAsync, useApiClient } from "@vc-shell/framework";
import { SalesRepClient, SalesRepRole } from "../../../../api_client/virtocommerce.salesrep";

export default () => {
  const { getApiClient } = useApiClient(SalesRepClient);

  const roles = ref<SalesRepRole[]>([]);

  const { loading: loadingRoles, action: loadRoles } = useAsync(async () => {
    const apiClient = await getApiClient();
    const result = await apiClient.getRoles();
    roles.value = result ?? [];
  });

  return {
    roles,
    loadRoles,
    loadingRoles,
  };
};
