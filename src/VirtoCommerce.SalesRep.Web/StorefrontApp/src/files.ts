import { toValue } from "vue";
import { downloadFile, Logger, useFetch } from "@vc-frontend/core";

// Revoked lazily so the new tab, or the download, can still read the object URL.
const OBJECT_URL_REVOKE_DELAY_MS = 60_000;

// Inline-renderable types only; .html/.svg etc. could run script on the storefront origin, so they're downloaded, never opened.
const INLINE_RENDERABLE_TYPES = new Set([
  "application/pdf",
  "image/png",
  "image/jpeg",
  "image/gif",
  "image/webp",
  "text/plain",
]);

export function isInlineRenderable(contentType?: string): boolean {
  const type = contentType?.toLowerCase();
  return !!type && INLINE_RENDERABLE_TYPES.has(type);
}

/**
 * A tab for a page that is not ready yet, opened in the click itself: a tab opened after an `await` is popup-blocked
 * by Safari/Firefox. Null when the browser blocked it anyway.
 */
export function openBlankTab(): Window | null {
  const tab = window.open("", "_blank");
  if (tab) {
    tab.opener = null; // sever the opener, as rel="noopener" would.
  }

  return tab;
}

/** Downloads a file the page built itself; downloadFile is for a file the server serves. */
export function saveFile(content: string, fileName: string, contentType: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: contentType }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), OBJECT_URL_REVOKE_DELAY_MS);
}

// A plain anchor carries no bearer token, so fetch through the authenticated path, then hand the tab the blob's object URL.
export async function openAuthorizedFile(fileUrl: string, contentType: string, fileName: string): Promise<void> {
  if (!isInlineRenderable(contentType)) {
    await downloadFile(fileUrl, fileName);
    return;
  }

  const tab = openBlankTab();

  try {
    const { data } = await useFetch(fileUrl).blob();
    let blob = toValue(data) as Blob;

    if (blob.type !== contentType) {
      blob = new Blob([blob], { type: contentType });
    }

    const url = URL.createObjectURL(blob);

    if (tab) {
      tab.location.href = url;
      setTimeout(() => URL.revokeObjectURL(url), OBJECT_URL_REVOKE_DELAY_MS);
    } else {
      // Popup blocked: fall back to a download.
      URL.revokeObjectURL(url);
      await downloadFile(fileUrl, fileName);
    }
  } catch (error) {
    // No second toast: useFetch already surfaced the HTTP error globally (same as Download).
    tab?.close();
    Logger.error(openAuthorizedFile.name, error);
  }
}
