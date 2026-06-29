import { computed, ref } from "vue";
import * as _ from "lodash-es";
import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import { SalesRepClient, SalesRepDetails } from "../../../../api_client/virtocommerce.salesrep";

function emptySalesRep(): SalesRepDetails {
  return {
    emails: [],
    phones: [],
    addresses: [],
    organizations: [],
    isLocked: false,
  } as unknown as SalesRepDetails;
}

export default () => {
  const { getApiClient } = useApiClient(SalesRepClient);

  const salesRep = ref<SalesRepDetails>(emptySalesRep());
  const originalSalesRep = ref<SalesRepDetails>(emptySalesRep());

  const resetSalesRep = () => {
    salesRep.value = _.cloneDeep(originalSalesRep.value);
  };

  const salesRepIsDirty = computed(() => !_.isEqual(salesRep.value, originalSalesRep.value));

  const { loading: loadingSalesRep, action: loadSalesRep } = useAsync<{ id: string }>(async (args?: { id: string }) => {
    if (args) {
      const apiClient = await getApiClient();
      const apiResult = await apiClient.get(args.id);
      if (apiResult) {
        salesRep.value = apiResult;
        originalSalesRep.value = _.cloneDeep(apiResult);
      }
    }
  });

  const { loading: savingSalesRep, action: saveSalesRep } = useAsync(async () => {
    const apiClient = await getApiClient();
    const saveable = _.cloneDeep(salesRep.value);

    const result = saveable.id ? await apiClient.update(saveable) : await apiClient.create(saveable);

    if (result) {
      salesRep.value = result;
      originalSalesRep.value = _.cloneDeep(result);
    }
  });

  const { loading: blockingSalesRep, action: blockSalesRep } = useAsync(async () => {
    if (salesRep.value.id) {
      const apiClient = await getApiClient();
      await apiClient.block(salesRep.value.id);
    }
  });

  const { loading: unblockingSalesRep, action: unblockSalesRep } = useAsync(async () => {
    if (salesRep.value.id) {
      const apiClient = await getApiClient();
      await apiClient.unblock(salesRep.value.id);
    }
  });

  // Block/Unblock are dedicated actions (like news Archive/Unarchive): only on a saved, pristine rep.
  const salesRepCanBlock = computed(
    () => !salesRepIsDirty.value && !!originalSalesRep.value.id && !originalSalesRep.value.isLocked,
  );
  const salesRepCanUnblock = computed(
    () => !salesRepIsDirty.value && !!originalSalesRep.value.id && !!originalSalesRep.value.isLocked,
  );

  return {
    salesRep,
    loadSalesRep,
    saveSalesRep,
    resetSalesRep,
    salesRepIsDirty,
    blockSalesRep,
    unblockSalesRep,
    salesRepCanBlock,
    salesRepCanUnblock,
    loadingOrSavingSalesRep: useLoading(loadingSalesRep, savingSalesRep, blockingSalesRep, unblockingSalesRep),
  };
};
