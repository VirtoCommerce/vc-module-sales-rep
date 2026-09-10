import { computed, ref } from "vue";
import * as _ from "lodash-es";
import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import {
  SalesRepDocumentsClient,
  SalesRepDocument,
  SalesRepDocumentMetadata,
  SalesRepDocumentSearchCriteria,
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
    // No single-document REST read — fetch through search pinned to the requested id.
    const result = await apiClient.search({ objectIds: [args.id], take: 1 } as SalesRepDocumentSearchCriteria);

    const found = result?.results?.[0];
    if (found) {
      setDocument(found);
    }
  });

  const { loading: savingMetadata, action: saveMetadata } = useAsync(async () => {
    if (!document.value.id) {
      return;
    }

    const apiClient = await getApiClient();
    // The endpoint is full-replace: omitted fields are cleared, so always send the complete metadata object.
    // Pin state is NOT part of it — the PUT preserves the current pin; only the pin endpoints change it.
    // displayName is the metadata name coalesced with the file name, so a value equal to the file name (or blank)
    // is sent as "no override" — the backend keeps falling back to the file name.
    const displayName = document.value.displayName?.trim();
    const result = await apiClient.updateMetadata(document.value.id, {
      name: displayName && displayName !== document.value.name ? displayName : undefined,
      category: document.value.category,
      summary: document.value.summary,
      pageCount: document.value.pageCount,
      previewUrl: document.value.previewUrl,
    } as SalesRepDocumentMetadata);

    if (result) {
      setDocument(result);
    }
  });

  // Pin/unpin return 204 (no body); re-fetch so IsPinned reflects the server (single-pin invariant
  // may also have flipped another document, but this blade only shows the current one).
  const { loading: pinningDocument, action: pinDocument } = useAsync(async () => {
    if (document.value.id) {
      const apiClient = await getApiClient();
      await apiClient.pin(document.value.id);
      await loadDocument({ id: document.value.id });
    }
  });

  const { loading: unpinningDocument, action: unpinDocument } = useAsync(async () => {
    if (document.value.id) {
      const apiClient = await getApiClient();
      await apiClient.unpin(document.value.id);
      await loadDocument({ id: document.value.id });
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
    pinDocument,
    unpinDocument,
    loadingOrSavingDocument: useLoading(
      loadingDocument,
      savingMetadata,
      deletingDocument,
      pinningDocument,
      unpinningDocument,
    ),
  };
};
