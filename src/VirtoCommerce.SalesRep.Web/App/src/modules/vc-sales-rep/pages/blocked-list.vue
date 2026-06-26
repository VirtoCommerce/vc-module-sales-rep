<template>
  <VcBlade
    :title="$t('VC_SALES_REP.MENU.BLOCKED')"
    width="100%"
  >
    <div class="tw-flex tw-flex-col tw-items-center tw-justify-center tw-h-full tw-text-center tw-gap-2 tw-p-8">
      <VcIcon
        icon="lucide-user-x"
        size="xxl"
        class="tw-text-[color:var(--neutrals-400)]"
      />
      <h2 class="tw-text-xl tw-font-semibold">{{ $t("VC_SALES_REP.MENU.BLOCKED") }}</h2>
      <p class="tw-text-[color:var(--neutrals-500)]">{{ $t("VC_SALES_REP.PAGES.PLACEHOLDER.SUBTITLE") }}</p>
    </div>
  </VcBlade>
</template>

<script lang="ts" setup>
import { VcBlade, VcIcon } from "@vc-shell/framework/ui";

defineBlade({
  url: "/blocked",
  name: "SalesRepsBlockedList",
  isWorkspace: true,
  menuItem: {
    title: "VC_SALES_REP.MENU.BLOCKED",
    icon: "lucide-user-x",
    priority: 30,
  },
});
</script>
