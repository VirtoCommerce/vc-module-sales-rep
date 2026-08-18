import { useAsync, useApiClient, useLoading } from "@vc-shell/framework";
import { SalesRepDocumentsClient, SalesRepDocument } from "../../../../api_client/virtocommerce.salesrep";

// UX-side filter only — the platform's IFileExtensionService white/blacklist re-validates every upload on the backend.
export const DOCUMENT_FILE_EXTENSIONS =
  ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.zip,.png,.jpg,.jpeg,.gif,.webp";

// Mirrors the backend ModuleConstants.Documents.CategoryMaxLength — the server rejects longer categories with 400.
export const CATEGORY_MAX_LENGTH = 32;

export interface DocumentUploadArgs {
  file: File;
  category: string;
  name?: string;
  summary?: string;
  pageCount?: number;
  previewUrl?: string;
}

const DOCUMENTS_SCOPE = "sales-rep-documents";

interface FileUploadResult {
  id?: string;
  name?: string;
  succeeded?: boolean;
  errorMessage?: string;
}

// Hand-built fetch: the upload endpoint belongs to the file-experience-api module (not this client), and
// download() would discard the response body. Auth is attached exactly like AuthApiBase.transformOptions.
export default () => {
  const { getApiClient } = useApiClient(SalesRepDocumentsClient);

  const getAuthHeaders = async (): Promise<Record<string, string>> => {
    const apiClient = await getApiClient();
    return apiClient.authToken ? { authorization: `Bearer ${apiClient.authToken}` } : {};
  };

  // Two steps: upload the bytes to the sales-rep-documents scope, then register the file in the library.
  const { loading: uploading, action: uploadDocument } = useAsync<DocumentUploadArgs, SalesRepDocument | undefined>(
    async (args?: DocumentUploadArgs) => {
      if (!args) {
        return undefined;
      }

      const formData = new FormData();
      formData.append("file", args.file, args.file.name);

      const uploadResponse = await fetch(`/api/files/${DOCUMENTS_SCOPE}`, {
        method: "POST",
        headers: await getAuthHeaders(),
        body: formData,
      });

      if (!uploadResponse.ok) {
        throw new Error(
          `Failed to upload "${args.file.name}": ${uploadResponse.status} ${await uploadResponse.text()}`,
        );
      }

      const [uploadedFile] = ((await uploadResponse.json()) ?? []) as FileUploadResult[];
      if (!uploadedFile?.id || uploadedFile.succeeded === false) {
        throw new Error(`Failed to upload "${args.file.name}": ${uploadedFile?.errorMessage ?? "no file id returned"}`);
      }

      const createResponse = await fetch("/api/sales-rep/documents", {
        method: "POST",
        headers: { ...(await getAuthHeaders()), "content-type": "application/json" },
        body: JSON.stringify({
          fileId: uploadedFile.id,
          category: args.category,
          name: args.name,
          summary: args.summary,
          pageCount: args.pageCount,
          previewUrl: args.previewUrl,
        }),
      });

      if (!createResponse.ok) {
        throw new Error(
          `Failed to register "${args.file.name}": ${createResponse.status} ${await createResponse.text()}`,
        );
      }

      return (await createResponse.json()) as SalesRepDocument;
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
