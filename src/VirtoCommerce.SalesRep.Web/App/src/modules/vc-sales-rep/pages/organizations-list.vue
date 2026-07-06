<template>
  <VcBlade
    :title="$t('VC_SALES_REP.MENU.ORGANIZATIONS')"
    width="100%"
  >
    <div class="tw-flex tw-flex-col tw-items-center tw-justify-center tw-h-full tw-text-center tw-gap-2 tw-p-8">
      <VcIcon
        icon="lucide-building-2"
        size="xxl"
        class="tw-text-[color:var(--neutrals-400)]"
      />
      <h2 class="tw-text-xl tw-font-semibold">{{ $t("VC_SALES_REP.MENU.ORGANIZATIONS") }}</h2>
      <p class="tw-text-[color:var(--neutrals-500)]">{{ $t("VC_SALES_REP.PAGES.PLACEHOLDER.SUBTITLE") }}</p>
    </div>
  </VcBlade>
</template>

<script lang="ts" setup>
import { VcBlade, VcIcon } from "@vc-shell/framework/ui";

defineBlade({
  url: "/organizations",
  name: "SalesRepOrganizationsList",
  isWorkspace: true,
  menuItem: {
    title: "VC_SALES_REP.MENU.ORGANIZATIONS",
    icon: "lucide-building-2",
    priority: 50,
  },
});
</script>
