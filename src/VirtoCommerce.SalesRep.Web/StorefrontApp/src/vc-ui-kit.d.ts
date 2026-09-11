// The ui-kit declares its table types in a `declare global` block, so they are ambient in the
// host and unreachable through the facade — a types-only package cannot re-export a global.
// The module reads exactly one of them, and it is structural, so mirroring it here is enough.
declare global {
  type VcTableSortDirectionType = "asc" | "desc";

  type VcTableSortInfoType = {
    column: string;
    direction: VcTableSortDirectionType;
  };
}

export {};
