<template>
  <VcBlade
    :loading="loading"
    :title="document.name || $t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TITLE')"
    :toolbar-items="bladeToolbar"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4 tw-p-3">
        <!-- Document info -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.BLOCKS.INFO')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcField
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.NAME')"
                :model-value="document.name"
                copyable
              />
              <VcField
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.CATEGORY')"
                :model-value="document.category"
              />
            </div>
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcField
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.SIZE')"
                :model-value="readableSize(document.size)"
              />
              <VcField
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.CONTENT_TYPE')"
                :model-value="document.contentType"
              />
            </div>
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcField
                class="tw-flex-1"
                type="date"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.CREATED_DATE')"
                :model-value="document.createdDate"
              />
              <VcField
                class="tw-flex-1"
                type="date"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.MODIFIED_DATE')"
                :model-value="document.modifiedDate"
              />
            </div>
          </div>
        </VcCard>

        <!-- Metadata -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.BLOCKS.METADATA')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <VcTextarea
              v-model="document.summary"
              style="--textarea-height: 80px"
              :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.SUMMARY')"
              :disabled="!hasWriteAccess"
            />
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcInput
                v-model="pageCountValue"
                type="number"
                class="tw-w-1/3"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.PAGE_COUNT')"
                :disabled="!hasWriteAccess"
              />
              <VcInput
                v-model="document.previewUrl"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.PREVIEW_URL')"
                :disabled="!hasWriteAccess"
              />
            </div>
          </div>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import {
  IBladeToolbar,
  usePermissions,
  usePopup,
  useBlade,
  useLoading,
  useBladeForm,
  readableSize,
} from "@vc-shell/framework";
import { useSalesRepDocumentDetails, useSalesRepDocumentTransfer, useSalesRepPermissions } from "../composables";
import { VcBlade, VcContainer, VcForm, VcCard, VcField, VcInput, VcTextarea } from "@vc-shell/framework/ui";

defineBlade({
  url: "/document-details",
  name: "SalesRepDocumentDetails",
});

const { t } = useI18n({ useScope: "global" });
const { param, callParent, closeSelf } = useBlade();
const { hasAccess } = usePermissions();
const { showConfirmation } = usePopup();

const {
  document,
  documentIsDirty,
  loadDocument,
  saveMetadata,
  resetDocument,
  deleteDocument,
  loadingOrSavingDocument,
} = useSalesRepDocumentDetails();
const { downloadDocument, transferring } = useSalesRepDocumentTransfer();
const { writeDocumentsPermission } = useSalesRepPermissions();

const hasWriteAccess = computed(() => hasAccess(writeDocumentsPermission));

const loading = useLoading(loadingOrSavingDocument, transferring);

// VcInput type="number" models a string; the API wants int?. Empty input = metadata cleared (undefined).
const pageCountValue = computed<string | undefined>({
  get: () => (document.value.pageCount == null ? undefined : String(document.value.pageCount)),
  set: (value) => {
    const parsed = Number.parseInt(value ?? "", 10);
    document.value.pageCount = Number.isNaN(parsed) ? undefined : parsed;
  },
});

const { canSave, setBaseline } = useBladeForm({
  data: document,
  closeConfirmMessage: computed(() => t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.ALERTS.CLOSE_CONFIRMATION")),
  canSaveOverride: computed(() => !!documentIsDirty.value),
});

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "save",
    icon: "lucide-save",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TOOLBAR.SAVE"),
    disabled: !canSave.value,
    clickHandler: async () => {
      await saveMetadata();
      setBaseline();
      callParent("reload");
    },
    isVisible: hasWriteAccess.value,
  },
  {
    id: "reset",
    icon: "lucide-undo-2",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TOOLBAR.RESET"),
    disabled: !documentIsDirty.value,
    clickHandler: async () => {
      resetDocument();
      setBaseline();
    },
    isVisible: hasWriteAccess.value,
  },
  {
    id: "download",
    icon: "lucide-download",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TOOLBAR.DOWNLOAD"),
    disabled: !document.value.url,
    clickHandler: async () => {
      await downloadDocument(document.value);
    },
  },
  {
    id: "delete",
    icon: "lucide-trash-2",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TOOLBAR.DELETE"),
    disabled: !document.value.id,
    clickHandler: async () => {
      const confirmed = await showConfirmation(
        t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.ALERTS.DELETE_CONFIRMATION", { name: document.value.name }),
      );
      if (confirmed) {
        await deleteDocument();
        callParent("reload");
        await closeSelf();
      }
    },
    isVisible: hasWriteAccess.value,
  },
]);

onMounted(async () => {
  if (param.value) {
    await loadDocument({ id: param.value });
  }
  setBaseline();
});
</script>
