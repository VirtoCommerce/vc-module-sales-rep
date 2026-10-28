<template>
  <!-- The applied filters as chips, each one removable, then one that removes them all. Nothing while none apply. -->
  <div v-if="chips.length" class="sales-rep-filter-chips">
    <VcChip
      v-for="chip in chips"
      :key="chip.id"
      color="secondary"
      closable
      :close-button-aria-label="t('sales_rep.filters.remove_filter', { label: chip.label })"
      @close="$emit('remove', chip)"
    >
      {{ chip.label }}
    </VcChip>

    <VcChip color="secondary" variant="outline" clickable @click="$emit('reset')">
      <span>{{ t("common.buttons.reset_filters") }}</span>

      <VcIcon name="reset" />
    </VcChip>
  </div>
</template>

<script setup lang="ts" generic="TChip extends SalesRepFilterChipType">
import { useI18n } from "vue-i18n";
import type { SalesRepFilterChipType } from "../types";

interface IProps {
  chips: TChip[];
}

defineEmits<{
  (event: "remove", chip: TChip): void;
  (event: "reset"): void;
}>();

defineProps<IProps>();

const { t } = useI18n();
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-filter-chips {
  @apply flex flex-wrap gap-x-3 gap-y-2;
}
</style>
