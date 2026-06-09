import { format } from "date-fns";
import type { RecordingSummary } from "../types";

/** Имя файла выгрузки записи: `perf-recording_<start>_<id8>.<ext>`, где start — момент старта
 *  записи (date-time, локальная TZ), id8 — первые 8 символов GUID (различить записи одного дня).
 *  Образец — `features/reports/export/exportFilename`. */
export function recordingFilename(recording: RecordingSummary, ext: string): string {
  const start = format(new Date(recording.startedAtUtc), "yyyy-MM-dd_HHmm");
  const idPart = recording.id.replace(/-/g, "").slice(0, 8);
  return `perf-recording_${start}_${idPart}.${ext}`;
}
