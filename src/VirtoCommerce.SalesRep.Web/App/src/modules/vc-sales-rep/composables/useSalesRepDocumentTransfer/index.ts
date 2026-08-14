import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import { SalesRepDocumentsClient, SalesRepDocument } from "../../../../api_client/virtocommerce.salesrep";

// UX-side filter only — the platform's IFileExtensionService white/blacklist re-validates every upload on the backend.
export const DOCUMENT_FILE_EXTENSIONS =
  ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.zip,.png,.jpg,.jpeg,.gif,.webp";

export interface DocumentUploadArgs {
  file: File;
  category: string;
  summary?: string;
}

// File transfer endpoints are called with a hand-built fetch instead of the generated SalesRepDocumentsClient:
// its upload() rejects omitted optional form fields (NSwag renders them as required-not-null) and its download()
// discards the response body. Both calls attach auth exactly like AuthApiBase.transformOptions does — a bearer
// header when the framework has wired a token onto the client, cookies otherwise — and use the same relative
// URLs, so they go through the identical pipeline as every other API call in this app.
export default () => {
  const { getApiClient } = useApiClient(SalesRepDocumentsClient);

  const getAuthHeaders = async (): Promise<Record<string, string>> => {
    const apiClient = await getApiClient();
    return apiClient.authToken ? { authorization: `Bearer ${apiClient.authToken}` } : {};
  };

  const { loading: uploading, action: uploadDocument } = useAsync<DocumentUploadArgs, SalesRepDocument | undefined>(
    async (args?: DocumentUploadArgs) => {
      if (!args) {
        return undefined;
      }

      const formData = new FormData();
      formData.append("file", args.file, args.file.name);
      formData.append("category", args.category);
      if (args.summary) {
        formData.append("summary", args.summary);
      }

      const response = await fetch("/api/sales-rep/documents", {
        method: "POST",
        headers: await getAuthHeaders(),
        body: formData,
      });

      if (!response.ok) {
        throw new Error(`Failed to upload "${args.file.name}": ${response.status} ${await response.text()}`);
      }

      return (await response.json()) as SalesRepDocument;
    },
  );

  const { loading: downloading, action: downloadDocument } = useAsync<SalesRepDocument>(
    async (document?: SalesRepDocument) => {
      if (!document?.url) {
        return;
      }

      const response = await fetch(document.url, { headers: await getAuthHeaders() });

      if (!response.ok) {
        throw new Error(`Failed to download "${document.name}": ${response.status}`);
      }

      const blobUrl = URL.createObjectURL(await response.blob());
      try {
        const link = window.document.createElement("a");
        link.href = blobUrl;
        link.download = document.name ?? "document";
        link.click();
      } finally {
        URL.revokeObjectURL(blobUrl);
      }
    },
  );

  return {
    uploadDocument,
    downloadDocument,
    transferring: useLoading(uploading, downloading),
  };
};
