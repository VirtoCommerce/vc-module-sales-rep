<template>
  <!-- lazy: otherwise the popover mounts at once and floating-ui's autoUpdate measures the trigger every frame, once
       per hint. -->
  <VcTooltip class="tracked-metric-hint" lazy placement="top">
    <!-- A focusable trigger of our own, so the hint opens from the keyboard as well as on hover. -->
    <template #default="{ triggerProps }">
      <span class="tracked-metric-hint__trigger" tabindex="0" v-bind="triggerProps">
        <VcIcon name="hourglass" :size="14" :label="t('sales_rep.activity.tracked_hint')" />
      </span>
    </template>

    <template #content>
      {{ t("sales_rep.activity.tracked_hint") }}
    </template>
  </VcTooltip>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

// Marks a tracked (GA) figure with the one thing a reader can act on: it can take up to 48 hours to appear.
const { t } = useI18n();
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.tracked-metric-hint {
  @apply inline-flex shrink-0 items-center self-center;

  // Flex, so the icon is centred instead of sitting on a text line.
  &__trigger {
    @apply flex items-center text-neutral-500;
  }
}
</style>
