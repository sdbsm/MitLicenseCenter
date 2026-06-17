import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { sizeScopeLabel, type SizeExportData } from "./sizeExport";

function fmt(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

/** Сериализация отчёта размера в книгу Excel (SheetJS) — `dynamic import` по клику, чтобы
 *  тяжёлая либа не попадала в основной бандл. Листы: «Сводка» (метаданные разреза),
 *  «Рост размера» (ряд итога во времени) и таблица разреза («Клиенты» для сводки / «Базы»
 *  для детализации). Размеры кладутся НАСТОЯЩИМИ числами (байты), чтобы в Excel работали
 *  сводные/графики. */
export async function toSizeXlsx(data: SizeExportData): Promise<Blob> {
  const XLSX = await import("xlsx");

  const summaryRows: (string | number)[][] = [
    ["Разрез", sizeScopeLabel(data.scope)],
    ["Период с", fmt(data.fromUtc)],
    ["Период по", fmt(data.toUtc)],
  ];
  const summarySheet = XLSX.utils.aoa_to_sheet(summaryRows);

  const growthRows: (string | number)[][] = [
    ["Момент", "Данные (байт)", "Журнал (байт)", "Итого (байт)"],
    ...data.points.map((p) => [fmt(p.atUtc), p.dataBytes, p.logBytes, p.totalBytes]),
  ];
  const growthSheet = XLSX.utils.aoa_to_sheet(growthRows);

  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, summarySheet, "Сводка");
  XLSX.utils.book_append_sheet(wb, growthSheet, "Рост размера");

  if (data.scope === "all") {
    const tenantRows: (string | number)[][] = [
      ["Клиент", "Итого (байт)", "Данные (байт)", "Журнал (байт)", "Число баз"],
      ...data.tenants.map((row) => [
        row.tenantName ?? "Без клиента",
        row.totalBytes,
        row.dataBytes,
        row.logBytes,
        row.databaseCount,
      ]),
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(tenantRows), "Клиенты");
  } else {
    const dbRows: (string | number)[][] = [
      ["База", "Итого (байт)", "Данные (байт)", "Журнал (байт)"],
      ...data.databases.map((db) => [db.databaseName, db.totalBytes, db.dataBytes, db.logBytes]),
    ];
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(dbRows), "Базы");
  }

  const buffer = XLSX.write(wb, { type: "array", bookType: "xlsx" }) as ArrayBuffer;
  return new Blob([buffer], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
}
