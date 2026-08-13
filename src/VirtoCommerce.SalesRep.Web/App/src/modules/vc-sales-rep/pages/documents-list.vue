<template>
  <VcBlade
    :loading="loadingDocuments"
    :title="$t('VC_SALES_REP.MENU.DOCUMENTS')"
    :toolbar-items="bladeToolbar"
    width="50%"
  >
    <VcDataTable
      v-model:active-item-id="selectedItemId"
      v-model:sort-field="sortField"
      v-model:sort-order="sortOrder"
      v-model:selection="localSelection"
      :items="documents"
      :total-count="pagination.totalCount"
      :pagination="pagination"
      :searchable="true"
      :selection-mode="'multiple'"
      :search-placeholder="$t('VC_SALES_REP.PAGES.DOCUMENTS.SEARCH.PLACEHOLDER')"
      state-key="VC_SALES_REP_DOCUMENTS"
      class="tw-grow tw-basis-0"
      @row-click="onItemClick"
      @pagination-click="pagination.goToPage"
      @search="onSearchChange"
      @filter="onFilterChange"
    >
      <VcColumn
        v-for="col in columns"
        :id="col.id"
        :key="col.id"
        :title="col.title"
        :field="col.field"
        :width="col.width"
        :always-visible="col.alwaysVisible"
        :visible="col.visible"
        :sortable="col.sortable"
        :type="col.type"
        :filter="col.id === 'category' ? categoryFilter : undefined"
        :mobile-position="col.mobilePosition"
        :mobile-role="col.mobileRole"
      >
        <template
          v-if="col.id === 'size'"
          #body="{ data }"
        >
          {{ readableSize((data as SalesRepDocument).size) }}
        </template>
      </VcColumn>
    </VcDataTable>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, ref, onMounted, watch } from "vue";
import { useDataTableSort, useDataTablePagination, useBlade, readableSize } from "@vc-shell/framework";
import { useSalesRepDocumentsUI, useSalesRepDocuments } from "../composables";
import { SalesRepDocument } from "../../../api_client/virtocommerce.salesrep";
import { VcBlade, VcDataTable, VcColumn, type SelectFilterConfig } from "@vc-shell/framework/ui";

const { param, exposeToChildren } = useBlade();

defineBlade({
  url: "/documents",
  name: "SalesRepDocumentsList",
  isWorkspace: true,
  menuItem: {
    title: "VC_SALES_REP.MENU.DOCUMENTS",
    icon: "lucide-library",
    priority: 70,
    // Any-of: readers get the read-only list, writers manage it, Administrator always passes.
    permissions: ["sales-rep-documents:read", "sales-rep-documents:write"],
  },
});

const {
  documents,
  documentsCount,
  pageIndex,
  searchQuery,
  categories,
  searchDocuments,
  loadCategories,
  deleteDocuments,
  loadingDocuments,
} = useSalesRepDocuments();

const { sortField, sortOrder, sortExpression } = useDataTableSort({
  initialField: "createdDate",
  initialDirection: "DESC",
});

const pagination = useDataTablePagination({
  pageSize: 20,
  totalCount: computed(() => documentsCount.value),
  onPageChange: ({ page }) => {
    pageIndex.value = page;
    return searchDocuments();
  },
});

const selectedItemId = ref<string>();
const localSelection = ref<SalesRepDocument[]>([]);
// Derive the id list straight from the selection source so the two can never drift apart
// (see sales-reps-list.vue for the rationale); clearing must always target localSelection.
const selectedIds = computed(() => localSelection.value.map((item) => item.id ?? "").filter(Boolean));

const resetSelection = () => {
  localSelection.value = [];
};

watch(
  () => param.value,
  (newVal) => {
    selectedItemId.value = newVal;
  },
  { immediate: true },
);

const { bladeToolbar, columns, openDetailsBlade, reOpenDetailsBlade } = useSalesRepDocumentsUI({
  selectedItemId,
  selectedIds,
  searchDocuments,
  loadCategories,
  deleteDocuments,
  resetSelection,
});

// Declarative column filter (framework convention): the table renders the dropdown and emits a flat
// backend payload via @filter; the actual filtering happens server-side through the search criteria.
const categoryFilter = computed(
  (): SelectFilterConfig => ({
    options: categories.value.map((x) => ({ value: x.name ?? "", label: `${x.name} (${x.count})` })),
  }),
);

const onFilterChange = async (event: { filters: Record<string, unknown> }) => {
  const raw = event.filters.category;
  const category = Array.isArray(raw) ? raw[0] : raw;
  searchQuery.value.category = typeof category === "string" && category ? category : undefined;
  pageIndex.value = 1;
  await searchDocuments();
};

const onItemClick = (event: { data: SalesRepDocument }) => {
  openDetailsBlade(event.data.id);
};

const onSearchChange = (keyword: string | undefined) => {
  searchQuery.value.keyword = keyword;
  pageIndex.value = 1;
  searchDocuments();
};

onMounted(async () => {
  await Promise.all([searchDocuments(), loadCategories()]);
});

watch(sortExpression, async (newSortValue) => {
  searchQuery.value.sort = newSortValue;
  await searchDocuments();
});

exposeToChildren({
  reload: async () => {
    await Promise.all([searchDocuments(), loadCategories()]);
  },
  reOpenDetailsBlade,
});
</script>
