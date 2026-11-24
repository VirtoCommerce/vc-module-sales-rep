<template>
  <!-- The applied filters as chips, each one removable, then one that removes them all. Nothing while none apply. -->
  <div v-if="chips.length" ref="rowRef" class="sales-rep-filter-chips">
    <!-- A long label, such as a customer's full name, ends in "…" instead of widening the page; the tooltip and the
         close button's name keep all of it. -->
    <VcChip
      v-for="(chip, index) in chips"
      :key="chip.id"
      color="secondary"
      closable
      truncate
      :title="chip.label"
      :close-button-aria-label="t('sales_rep.filters.remove_filter', { label: chip.label })"
      @close="remove(chip, index)"
    >
      {{ chip.label }}
    </VcChip>

    <VcChip color="secondary" variant="outline" clickable @click="reset">
      <span>{{ t("common.buttons.reset_filters") }}</span>

      <VcIcon name="reset" />
    </VcChip>
  </div>
</template>

<script setup lang="ts" generic="TChip extends SalesRepFilterChipType">
import { nextTick, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import type { SalesRepFilterChipType } from "../types";

interface IProps {
  chips: TChip[];
  /** Takes the keyboard focus once the row empties under it: its last chip closed, or every chip reset. */
  fallbackFocus?: () => void;
}

const emit = defineEmits<{
  (event: "remove", chip: TChip): void;
  (event: "reset"): void;
}>();

const props = defineProps<IProps>();

const { t } = useI18n();

const rowRef = useTemplateRef<HTMLElement>("rowRef");

// The pressed button leaves with its chip, which would drop a keyboard user's focus to <body>, back at the top of the
// page. So focus goes on to the chip that takes its place, else the new last one, else past the emptied row.
function remove(chip: TChip, index: number): void {
  const fromKeyboard = isKeyboardFocus();
  emit("remove", chip);
  if (fromKeyboard) {
    void nextTick(() => focusChip(index));
  }
}

function reset(): void {
  const fromKeyboard = isKeyboardFocus();
  emit("reset");
  if (fromKeyboard) {
    void nextTick(() => props.fallbackFocus?.());
  }
}

// The close button of the chip now at that place, or of the last one; none left, the row is gone.
function focusChip(index: number): void {
  const closeButtons = rowRef.value?.querySelectorAll<HTMLElement>(".vc-chip__close-button") ?? [];
  const target = closeButtons[Math.min(index, closeButtons.length - 1)];
  if (target) {
    target.focus();
  } else {
    props.fallbackFocus?.();
  }
}

// Only focus a key press brought, which the browser rings (:focus-visible). A click focuses without the ring, or not at
// all, and nothing moves then: a tap must not open a phone's on-screen keyboard on the search box.
function isKeyboardFocus(): boolean {
  return document.activeElement?.matches(":focus-visible") ?? false;
}
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-filter-chips {
  @apply flex flex-wrap gap-x-3 gap-y-2;
}
</style>
