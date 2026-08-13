import { computed, ref } from "vue";
import * as _ from "lodash-es";
import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import {
  SalesRepDocumentsClient,
  SalesRepDocument,
  SalesRepDocumentMetadata,
} from "../../../../api_client/virtocommerce.salesrep";

export default () => {
  const { getApiClient } = useApiClient(SalesRepDocumentsClient);

  const document = ref<SalesRepDocument>({});
  const originalDocument = ref<SalesRepDocument>({});

  const documentIsDirty = computed(() => !_.isEqual(document.value, originalDocument.value));

  const setDocument = (value: SalesRepDocument) => {
    document.value = value;
    originalDocument.value = _.cloneDeep(value);
  };

  const resetDocument = () => {
    document.value = _.cloneDeep(originalDocument.value);
  };

  const { loading: loadingDocument, action: loadDocument } = useAsync<{ id: string }>(async (args?: { id: string }) => {
    if (!args) {
      return;
    }

    const apiClient = await getApiClient();
    const result = await apiClient.getInfo(args.id);

    if (result) {
      setDocument(result);
    }
  });

  const { loading: savingMetadata, action: saveMetadata } = useAsync(async () => {
    if (!document.value.id) {
      return;
    }

    const apiClient = await getApiClient();
    const result = await apiClient.updateMetadata(document.value.id, {
      summary: document.value.summary,
      pageCount: document.value.pageCount,
      previewUrl: document.value.previewUrl,
    } as SalesRepDocumentMetadata);

    if (result) {
      setDocument(result);
    }
  });

  const { loading: deletingDocument, action: deleteDocument } = useAsync(async () => {
    if (document.value.id) {
      const apiClient = await getApiClient();
      await apiClient.delete([document.value.id]);
    }
  });

  return {
    document,
    documentIsDirty,
    loadDocument,
    saveMetadata,
    resetDocument,
    deleteDocument,
    loadingOrSavingDocument: useLoading(loadingDocument, savingMetadata, deletingDocument),
  };
};
