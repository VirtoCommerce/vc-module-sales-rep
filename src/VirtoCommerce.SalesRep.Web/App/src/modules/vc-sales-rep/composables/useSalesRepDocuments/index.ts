import { ref } from "vue";
import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import {
  SalesRepDocumentsClient,
  SalesRepDocument,
  SalesRepDocumentCategory,
  SalesRepDocumentSearchCriteria,
} from "../../../../api_client/virtocommerce.salesrep";

export default () => {
  const pageSize = 20;

  const { getApiClient } = useApiClient(SalesRepDocumentsClient);

  const searchQuery = ref<SalesRepDocumentSearchCriteria>({} as SalesRepDocumentSearchCriteria);
  const documents = ref<SalesRepDocument[]>([]);
  const documentsCount = ref(0);
  const pageIndex = ref(1);
  const categories = ref<SalesRepDocumentCategory[]>([]);

  const { loading: searching, action: searchDocuments } = useAsync(async () => {
    const apiClient = await getApiClient();
    const apiResult = await apiClient.search({
      ...(searchQuery.value ?? {}),
      take: pageSize,
      skip: pageSize * (pageIndex.value - 1),
    } as SalesRepDocumentSearchCriteria);

    if (apiResult) {
      documents.value = apiResult.results ?? [];
      documentsCount.value = apiResult.totalCount ?? 0;
    }
  });

  const { loading: loadingCategories, action: loadCategories } = useAsync(async () => {
    const apiClient = await getApiClient();
    categories.value = (await apiClient.getCategories()) ?? [];
  });

  const { loading: deleting, action: deleteDocuments } = useAsync<{ ids: string[] }>(
    async (args?: { ids: string[] }) => {
      if (args) {
        const apiClient = await getApiClient();
        await apiClient.delete(args.ids);
      }
    },
  );

  return {
    documents,
    documentsCount,
    pageSize,
    pageIndex,
    searchQuery,
    categories,
    searchDocuments,
    loadCategories,
    deleteDocuments,
    loadingDocuments: useLoading(searching, loadingCategories, deleting),
  };
};
