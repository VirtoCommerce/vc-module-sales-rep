<template>
  <VcBlade
    :loading="loading"
    :title="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.TITLE')"
    :toolbar-items="bladeToolbar"
    width="40%"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4 tw-p-3">
        <!-- Files -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.BLOCKS.FILES')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <VcFileUpload
              multiple
              :accept="DOCUMENT_FILE_EXTENSIONS"
              :error-message="fileTypeError"
              @upload="onFilesSelected"
            />
            <div
              v-for="(file, index) in pendingFiles"
              :key="`${file.name}_${index}`"
              class="tw-flex tw-flex-row tw-items-center tw-justify-between tw-gap-2 tw-rounded tw-border tw-border-solid tw-border-[color:var(--neutrals-200)] tw-px-3 tw-py-2"
            >
              <div class="tw-flex tw-flex-row tw-items-center tw-gap-2 tw-min-w-0">
                <VcIcon icon="lucide-file" />
                <span class="tw-truncate">{{ file.name }}</span>
                <span class="tw-shrink-0 tw-text-xs tw-text-[color:var(--neutrals-500)]">
                  {{ readableSize(file.size) }}
                </span>
              </div>
              <VcButton
                icon="lucide-trash-2"
                text
                @click="removeFile(index)"
              />
            </div>
          </div>
        </VcCard>

        <!-- Category & metadata -->
        <VcCard>
          <!-- Hint lives in the header itself; single-file-only fields (display name, pages,
               preview) disable themselves for batches. -->
          <template #header>
            <span>{{ $t("VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.BLOCKS.DETAILS") }}</span>
            <span class="tw-ml-2 tw-text-xs tw-font-normal tw-text-[color:var(--neutrals-500)]">
              {{ $t("VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.BLOCKS.DETAILS_HINT") }}
            </span>
          </template>
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcSelect
                v-model="selectedCategory"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.CATEGORY')"
                :placeholder="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.CATEGORY_PLACEHOLDER')"
                :options="categoryOptions"
                option-value="id"
                option-label="title"
                :disabled="!!newCategory"
                clearable
              />
              <VcInput
                v-model="newCategory"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.NEW_CATEGORY')"
                :placeholder="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.NEW_CATEGORY_PLACEHOLDER')"
                :hint="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.NEW_CATEGORY_HINT')"
                :maxlength="CATEGORY_MAX_LENGTH"
              />
            </div>
            <VcInput
              v-model="displayName"
              :label="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.DISPLAY_NAME')"
              :placeholder="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.DISPLAY_NAME_PLACEHOLDER')"
              :disabled="pendingFiles.length !== 1"
            />
            <VcTextarea
              v-model="summary"
              style="--textarea-height: 60px"
              :label="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.SUMMARY')"
            />
            <!-- Per-file values, like the display name: meaningless for a batch upload. -->
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcInput
                v-model="pageCount"
                type="number"
                class="tw-w-1/3"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.PAGE_COUNT')"
                :disabled="pendingFiles.length !== 1"
              />
              <VcInput
                v-model="previewUrl"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.PREVIEW_URL')"
                :disabled="pendingFiles.length !== 1"
              />
            </div>
          </div>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { IBladeToolbar, useBlade, useLoading, readableSize } from "@vc-shell/framework";
import {
  useSalesRepDocuments,
  useSalesRepDocumentTransfer,
  DOCUMENT_FILE_EXTENSIONS,
  CATEGORY_MAX_LENGTH,
} from "../composables";
import {
  VcBlade,
  VcContainer,
  VcForm,
  VcCard,
  VcFileUpload,
  VcSelect,
  VcInput,
  VcTextarea,
  VcButton,
  VcIcon,
} from "@vc-shell/framework/ui";

defineBlade({
  url: "/document-upload",
  name: "SalesRepDocumentUpload",
});

const { t } = useI18n({ useScope: "global" });
const { callParent, closeSelf } = useBlade();

const { categories, loadCategories, loadingDocuments } = useSalesRepDocuments();
const { uploadDocument, transferring } = useSalesRepDocumentTransfer();

const loading = useLoading(loadingDocuments, transferring);

const pendingFiles = ref<File[]>([]);
const fileTypeError = ref<string>();
const selectedCategory = ref<string>();
const newCategory = ref<string>();
const displayName = ref<string>();
const summary = ref<string>();
const pageCount = ref<number>();
const previewUrl = ref<string>();

const categoryOptions = computed(() => categories.value.map((x) => ({ id: x.name, title: x.name })));

// Category = target subfolder: either an existing one, or a fresh name typed in (which creates it on upload).
const effectiveCategory = computed(() => newCategory.value?.trim() || selectedCategory.value || "");

const allowedExtensions = new Set(DOCUMENT_FILE_EXTENSIONS.split(","));

const extensionOf = (fileName: string) => {
  const dot = fileName.lastIndexOf(".");
  return dot >= 0 ? fileName.slice(dot).toLowerCase() : "";
};

// Checked here (not via VcFileUpload rules) so drag-and-drop is covered too — drops bypass both `accept` and rules.
const onFilesSelected = (files: FileList) => {
  const selected = Array.from(files);
  const rejected = selected.filter((file) => !allowedExtensions.has(extensionOf(file.name)));
  fileTypeError.value = rejected.length
    ? t("VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.FORM.FILE_TYPE_NOT_ALLOWED", {
        files: rejected.map((file) => file.name).join(", "),
        types: DOCUMENT_FILE_EXTENSIONS.replace(/,/g, ", "),
      })
    : undefined;
  pendingFiles.value = [...pendingFiles.value, ...selected.filter((file) => !rejected.includes(file))];
};

const removeFile = (index: number) => {
  pendingFiles.value.splice(index, 1);
};

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "upload",
    icon: "lucide-upload",
    title: t("VC_SALES_REP.PAGES.DOCUMENT_UPLOAD.TOOLBAR.UPLOAD"),
    disabled: pendingFiles.value.length === 0 || !effectiveCategory.value,
    clickHandler: async () => {
      // Sequential on purpose: uploads share the category subfolder, and one clear failure beats a burst
      // of parallel half-failures.
      // Display name, page count and preview URL only make sense for a single file (inputs disabled otherwise).
      const singleFile = pendingFiles.value.length === 1;
      const name = singleFile ? displayName.value?.trim() || undefined : undefined;
      const pages = singleFile && pageCount.value != null && pageCount.value > 0 ? Number(pageCount.value) : undefined;
      const preview = singleFile ? previewUrl.value?.trim() || undefined : undefined;
      for (const file of pendingFiles.value) {
        await uploadDocument({
          file,
          category: effectiveCategory.value,
          name,
          summary: summary.value,
          pageCount: pages,
          previewUrl: preview,
        });
      }
      pendingFiles.value = [];
      callParent("reload");
      await closeSelf();
    },
  },
]);

onMounted(async () => {
  await loadCategories();
});
</script>
