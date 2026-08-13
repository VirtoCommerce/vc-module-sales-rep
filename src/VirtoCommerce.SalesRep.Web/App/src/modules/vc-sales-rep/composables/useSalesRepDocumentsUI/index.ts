import { ref, Ref, computed, ComputedRef } from "vue";
import { IBladeToolbar, useBlade, usePopup, usePermissions } from "@vc-shell/framework";
import { useI18n } from "vue-i18n";
import useSalesRepPermissions from "../useSalesRepPermissions";

interface IDocumentColumn {
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
  selectedIds: ComputedRef<string[]>;
  searchDocuments: () => Promise<void>;
  loadCategories: () => Promise<void>;
  deleteDocuments: (args: { ids: string[] }) => Promise<void>;
  resetSelection: () => void;
}) => {
  const { t } = useI18n({ useScope: "global" });
  const { showConfirmation } = usePopup();
  const { openBlade, closeChildren } = useBlade();
  const { hasAccess } = usePermissions();
  const { writeDocumentsPermission } = useSalesRepPermissions();

  const bladeToolbar = computed((): IBladeToolbar[] => [
    {
      id: "refresh",
      title: t("VC_SALES_REP.PAGES.DOCUMENTS.TOOLBAR.REFRESH"),
      icon: "lucide-refresh-cw",
      async clickHandler() {
        await Promise.all([options.searchDocuments(), options.loadCategories()]);
      },
    },
    {
      id: "upload",
      title: t("VC_SALES_REP.PAGES.DOCUMENTS.TOOLBAR.UPLOAD"),
      icon: "lucide-upload",
      clickHandler: async () => {
        openUploadBlade();
      },
      isVisible: () => hasAccess(writeDocumentsPermission),
    },
    {
      id: "delete",
      title: t("VC_SALES_REP.PAGES.DOCUMENTS.TOOLBAR.DELETE"),
      icon: "lucide-trash-2",
      disabled: options.selectedIds.value.length === 0,
      clickHandler: async () => {
        const confirmed = await showConfirmation(
          t("VC_SALES_REP.PAGES.DOCUMENTS.ALERTS.DELETE_SELECTED_CONFIRMATION_MESSAGE", {
            count: options.selectedIds.value.length,
          }),
        );
        if (confirmed) {
          await closeChildren();
          await options.deleteDocuments({ ids: options.selectedIds.value });
          // Clear the selection SOURCE (not the derived ids): this also resets the data table's
          // internal selection, so the just-deleted rows can't linger and get re-submitted.
          options.resetSelection();
          await Promise.all([options.searchDocuments(), options.loadCategories()]);
        }
      },
      isVisible: () => hasAccess(writeDocumentsPermission),
    },
  ]);

  const columns = ref<IDocumentColumn[]>([
    {
      id: "name",
      title: computed(() => t("VC_SALES_REP.PAGES.DOCUMENTS.TABLE.HEADER.NAME")),
      alwaysVisible: true,
      sortable: true,
      width: "35%",
      mobilePosition: "top-left",
      mobileRole: "title",
    },
    {
      id: "category",
      title: computed(() => t("VC_SALES_REP.PAGES.DOCUMENTS.TABLE.HEADER.CATEGORY")),
      alwaysVisible: true,
      sortable: false,
      width: "20%",
      mobilePosition: "top-right",
    },
    {
      id: "size",
      title: computed(() => t("VC_SALES_REP.PAGES.DOCUMENTS.TABLE.HEADER.SIZE")),
      alwaysVisible: true,
      sortable: true,
      width: "15%",
      mobilePosition: "bottom-left",
    },
    {
      id: "modifiedDate",
      title: computed(() => t("VC_SALES_REP.PAGES.DOCUMENTS.TABLE.HEADER.MODIFIED_DATE")),
      alwaysVisible: true,
      sortable: true,
      width: "15%",
      type: "datetime",
      mobilePosition: "bottom-right",
    },
    {
      id: "createdDate",
      title: computed(() => t("VC_SALES_REP.PAGES.DOCUMENTS.TABLE.HEADER.CREATED_DATE")),
      visible: false,
      sortable: true,
      width: "15%",
      type: "datetime",
    },
  ]);

  const openDetailsBlade = (id: string | undefined) => {
    if (!id) {
      return;
    }
    openBlade({
      name: "SalesRepDocumentDetails",
      param: id,
      onOpen() {
        options.selectedItemId.value = id;
      },
      onClose() {
        options.selectedItemId.value = undefined;
      },
    });
  };

  const openUploadBlade = () => {
    openBlade({
      name: "SalesRepDocumentUpload",
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
    openUploadBlade,
    reOpenDetailsBlade,
  };
};
