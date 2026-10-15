// The ui-kit declares these in `declare global` blocks, so they are ambient in the host and unreachable
// through the facade, whose contract inlines them as private types. Each is structural, and the facade
// types the props they are passed to, so a mirror that drifts from the host fails type-check.
import type { RouteLocationRaw } from "vue-router";

declare global {
  type VcMainColorType = "primary" | "secondary" | "neutral" | "accent" | "info" | "success" | "warning" | "danger";

  type VcChipColorType = VcMainColorType;
  type VcChipVariantType =
    "solid" | "soft" | "outline" | "surface" | "ghost" | "tonal" | "solid-light" | "outline-dark";

  type VcCalendarSizeType = "xs" | "sm" | "md";
  type VcDateRangeType = { start?: string; end?: string };
  type VcDateRangePickerLayoutType = "combined" | "split";

  type VcTableSortDirectionType = "asc" | "desc";

  type VcTableSortInfoType = {
    column: string;
    direction: VcTableSortDirectionType;
  };

  interface IBreadcrumb {
    title: string;
    route?: RouteLocationRaw;
  }
}

export {};
