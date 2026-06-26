import { ref, Ref, computed, ComputedRef } from "vue";
import { IBladeToolbar, useBlade, usePopup, usePermissions } from "@vc-shell/framework";
import { useI18n } from "vue-i18n";
import useSalesRepPermissions from "../useSalesRepPermissions";

interface ISalesRepColumn {
  id: string;
  title: ComputedRef<string>;
  field?: string;
  width?: string | number;
  alwaysVisible?: boolean;
  visible?: boolean;
  sortable?: boolean;
  type?: "datetime" | "status-icon" | "number";
  mobilePosition?: "top-left" | "top-right" | "bottom-left" | "bottom-right";
  mobileRole?: "title" | "image" | "field" | "status";
}

export default (options: {
  selectedItemId: Ref<string | undefined>;
  selectedIds: Ref<string[]>;
  searchSalesReps: () => Promise<void>;
  deleteSalesReps: (args: { ids: string[] }) => Promise<void>;
}) => {
  const { t } = useI18n({ useScope: "global" });
  const { showConfirmation } = usePopup();
  const { openBlade, closeChildren } = useBlade();
  const { hasAccess } = usePermissions();
  const { createSalesRepPermission, deleteSalesRepPermission } = useSalesRepPermissions();

  const bladeToolbar = computed((): IBladeToolbar[] => [
    {
      id: "refresh",
      title: t("VC_SALES_REP.PAGES.LIST.TOOLBAR.REFRESH"),
      icon: "lucide-refresh-cw",
      async clickHandler() {
        await options.searchSalesReps();
      },
    },
    {
      id: "add",
      title: t("VC_SALES_REP.PAGES.LIST.TOOLBAR.ADD"),
      icon: "lucide-plus",
      clickHandler: async () => {
        openDetailsBlade(undefined);
      },
      isVisible: () => hasAccess(createSalesRepPermission),
    },
    {
      id: "delete",
      title: t("VC_SALES_REP.PAGES.LIST.TOOLBAR.DELETE"),
      icon: "lucide-trash-2",
      disabled: options.selectedIds.value.length === 0,
      clickHandler: async () => {
        const confirmed = await showConfirmation(
          t("VC_SALES_REP.PAGES.LIST.ALERTS.DELETE_SELECTED_CONFIRMATION_MESSAGE", {
            count: options.selectedIds.value.length,
          }),
        );
        if (confirmed) {
          await closeChildren();
          await options.deleteSalesReps({ ids: options.selectedIds.value });
          options.selectedIds.value = [];
          await options.searchSalesReps();
        }
      },
      isVisible: () => hasAccess(deleteSalesRepPermission),
    },
  ]);

  const columns = ref<ISalesRepColumn[]>([
    {
      id: "fullName",
      title: computed(() => t("VC_SALES_REP.PAGES.LIST.TABLE.HEADER.NAME")),
      alwaysVisible: true,
      sortable: true,
      width: "30%",
      mobilePosition: "top-left",
    },
    {
      id: "email",
      title: computed(() => t("VC_SALES_REP.PAGES.LIST.TABLE.HEADER.EMAIL")),
      alwaysVisible: true,
      sortable: true,
      width: "30%",
      mobilePosition: "top-right",
    },
    {
      id: "organizationsCount",
      title: computed(() => t("VC_SALES_REP.PAGES.LIST.TABLE.HEADER.ORGANIZATIONS")),
      alwaysVisible: true,
      sortable: true,
      width: "15%",
      type: "number",
      mobilePosition: "bottom-left",
    },
    {
      id: "isLocked",
      title: computed(() => t("VC_SALES_REP.PAGES.LIST.TABLE.HEADER.BLOCKED")),
      alwaysVisible: true,
      sortable: true,
      width: "10%",
      type: "status-icon",
      mobileRole: "status",
    },
    {
      id: "hasGlobalSalesRepRole",
      title: computed(() => t("VC_SALES_REP.PAGES.LIST.TABLE.HEADER.GLOBAL_ROLE")),
      visible: false,
      sortable: false,
      width: "10%",
      type: "status-icon",
    },
    {
      id: "createdDate",
      title: computed(() => t("VC_SALES_REP.PAGES.LIST.TABLE.HEADER.CREATED_DATE")),
      visible: false,
      sortable: true,
      width: "20%",
      type: "datetime",
    },
  ]);

  const openDetailsBlade = (id: string | undefined) => {
    openBlade({
      name: "SalesRepDetails",
      param: id ?? undefined,
      onOpen() {
        options.selectedItemId.value = id ?? undefined;
      },
      onClose() {
        options.selectedItemId.value = undefined;
      },
    });
  };

  const reOpenDetailsBlade = async (id: string) => {
    await closeChildren();
    openDetailsBlade(id);
  };

  return {
    bladeToolbar,
    columns,
    openDetailsBlade,
    reOpenDetailsBlade,
  };
};
