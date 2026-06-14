// Расширение ColumnMeta (headClassName/cellClassName/label) — side-effect import,
// чтобы аугментация типов подхватилась везде, где используется DataTable.
import "./types";

export { DataTable, type DataTableProps } from "./DataTable";
export { DataTableToolbar } from "./DataTableToolbar";
export { DataTableColumnHeader } from "./DataTableColumnHeader";
export { useTableDensity, type TableDensity } from "./useTableDensity";
export { useUrlTableFilters } from "./useUrlTableFilters";
