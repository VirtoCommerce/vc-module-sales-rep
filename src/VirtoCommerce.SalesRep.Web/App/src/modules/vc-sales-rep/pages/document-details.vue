<template>
  <VcBlade
    :loading="loading"
    :title="document.displayName || $t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TITLE')"
    :toolbar-items="bladeToolbar"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4 tw-p-3">
        <!-- Document info -->
        <VcCard :header="document.displayName || $t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.BLOCKS.INFO')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <VcField
              :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.FILE_NAME')"
              :model-value="document.name"
              copyable
            />
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
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcSelect
                v-model="selectedCategory"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.CATEGORY')"
                :placeholder="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.CATEGORY_PLACEHOLDER')"
                :options="categoryOptions"
                option-value="id"
                option-label="title"
                :disabled="!hasWriteAccess || !!newCategory"
                clearable
              />
              <VcInput
                v-model="newCategory"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.NEW_CATEGORY')"
                :placeholder="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.NEW_CATEGORY_PLACEHOLDER')"
                :hint="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.NEW_CATEGORY_HINT')"
                :maxlength="CATEGORY_MAX_LENGTH"
                :disabled="!hasWriteAccess"
              />
            </div>
            <VcInput
              v-model="document.displayName"
              :label="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.DISPLAY_NAME')"
              :hint="$t('VC_SALES_REP.PAGES.DOCUMENT_DETAILS.FORM.DISPLAY_NAME_HINT')"
              :disabled="!hasWriteAccess"
            />
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
import { computed, onMounted, ref, watch } from "vue";
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
import {
  useSalesRepDocuments,
  useSalesRepDocumentDetails,
  useSalesRepDocumentTransfer,
  useSalesRepPermissions,
  CATEGORY_MAX_LENGTH,
} from "../composables";
import { VcBlade, VcContainer, VcForm, VcCard, VcField, VcInput, VcSelect, VcTextarea } from "@vc-shell/framework/ui";

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
  pinDocument,
  unpinDocument,
  loadingOrSavingDocument,
} = useSalesRepDocumentDetails();
const { downloadDocument, transferring } = useSalesRepDocumentTransfer();
const { categories, loadCategories } = useSalesRepDocuments();
const { writeDocumentsPermission } = useSalesRepPermissions();

const hasWriteAccess = computed(() => hasAccess(writeDocumentsPermission));

const loading = useLoading(loadingOrSavingDocument, transferring);

// Category editor mirrors the upload blade: pick an existing category, or type a new name which overrides
// the selection. Both feed document.category (the single value the full-replace metadata PUT sends).
const selectedCategory = ref<string | undefined>();
const newCategory = ref("");

const categoryOptions = computed(() => categories.value.map((x) => ({ id: x.name, title: x.name })));

const syncCategoryEditor = () => {
  selectedCategory.value = document.value.category;
  newCategory.value = "";
};

watch([selectedCategory, newCategory], () => {
  const typed = newCategory.value.trim();
  document.value.category = typed ? typed : selectedCategory.value;
});

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
      syncCategoryEditor();
      await loadCategories();
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
      syncCategoryEditor();
      setBaseline();
    },
    isVisible: hasWriteAccess.value,
  },
  {
    id: "pin",
    icon: "lucide-pin",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TOOLBAR.PIN"),
    disabled: !document.value.id || documentIsDirty.value,
    clickHandler: async () => {
      await pinDocument();
      setBaseline();
      callParent("reload");
    },
    isVisible: hasWriteAccess.value && !document.value.isPinned,
  },
  {
    id: "unpin",
    icon: "lucide-pin-off",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.TOOLBAR.UNPIN"),
    disabled: !document.value.id || documentIsDirty.value,
    clickHandler: async () => {
      await unpinDocument();
      setBaseline();
      callParent("reload");
    },
    isVisible: hasWriteAccess.value && !!document.value.isPinned,
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
        t("VC_SALES_REP.PAGES.DOCUMENT_DETAILS.ALERTS.DELETE_CONFIRMATION", { name: document.value.displayName }),
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
  await loadCategories();
  syncCategoryEditor();
  setBaseline();
});
</script>
