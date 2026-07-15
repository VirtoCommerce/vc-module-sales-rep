<template>
  <VcBlade
    :loading="loadingSalesReps"
    :title="$t('VC_SALES_REP.MENU.SALES_REPS')"
    :toolbar-items="bladeToolbar"
    width="50%"
  >
    <VcDataTable
      :items="salesReps"
      :total-count="pagination.totalCount"
      :pagination="pagination"
      :searchable="true"
      :selection-mode="'multiple'"
      :search-placeholder="$t('VC_SALES_REP.PAGES.LIST.SEARCH.PLACEHOLDER')"
      state-key="VC_SALES_REP"
      class="tw-grow tw-basis-0"
      v-model:active-item-id="selectedItemId"
      v-model:sort-field="sortField"
      v-model:sort-order="sortOrder"
      v-model:selection="localSelection"
      @row-click="onItemClick"
      @pagination-click="pagination.goToPage"
      @search="onSearchChange"
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
        :mobile-position="col.mobilePosition"
        :mobile-role="col.mobileRole"
      />
    </VcDataTable>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, ref, onMounted, watch } from "vue";
import { useDataTableSort, useDataTablePagination, useBlade } from "@vc-shell/framework";
import { useSalesRepListUI, useSalesRepList } from "../composables";
import { SalesRepListItem } from "../../../api_client/virtocommerce.salesrep";
import { VcBlade, VcDataTable, VcColumn } from "@vc-shell/framework/ui";

const { param, exposeToChildren } = useBlade();

defineBlade({
  url: "/sales-reps",
  name: "SalesRepsList",
  isWorkspace: true,
  menuItem: {
    title: "VC_SALES_REP.MENU.SALES_REPS",
    icon: "lucide-users",
    priority: 20,
  },
});

const { salesReps, salesRepsCount, pageIndex, searchQuery, searchSalesReps, deleteSalesReps, loadingSalesReps } =
  useSalesRepList();

const { sortField, sortOrder, sortExpression } = useDataTableSort({
  initialField: "fullName",
  initialDirection: "ASC",
});

const pagination = useDataTablePagination({
  pageSize: 20,
  totalCount: computed(() => salesRepsCount.value),
  onPageChange: ({ page }) => {
    pageIndex.value = page;
    return searchSalesReps();
  },
});

const selectedItemId = ref<string>();
const localSelection = ref<SalesRepListItem[]>([]);
// Derive the id list straight from the selection source so the two can never drift apart
// (e.g. retain ids of rows that were already deleted). The table's internal selection is
// only reset when this source is cleared, so clearing must always target localSelection.
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

const { bladeToolbar, columns, openDetailsBlade, reOpenDetailsBlade } = useSalesRepListUI({
  selectedItemId,
  selectedIds,
  searchSalesReps,
  deleteSalesReps,
  resetSelection,
});

const onItemClick = (event: { data: SalesRepListItem }) => {
  openDetailsBlade(event.data.id);
};

const onSearchChange = (keyword: string | undefined) => {
  searchQuery.value.keyword = keyword;
  searchSalesReps();
};

onMounted(async () => {
  await searchSalesReps();
});

watch(sortExpression, async (newSortValue) => {
  searchQuery.value.sort = newSortValue;
  await searchSalesReps();
});

exposeToChildren({
  reload: searchSalesReps,
  reOpenDetailsBlade,
});
</script>
