import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import {
  recordingDetailSchema,
  recordingsPagedSchema,
  recordingSummarySchema,
  type RecordingSummary,
} from "./types";

export const recordingsQueryKey = ["performance", "recordings"] as const;
export const recordingDetailQueryKey = (id: string) => ["performance", "recordings", id] as const;

// Список расследований (MLC-070/071, ADR-26). Чтение = Viewer. Поллим 5с, чтобы пока идёт запись
// её индикатор и счётчик сэмплов оставались свежими (в одном ритме с live-источниками раздела);
// расследований немного и persisted-список дёшев. Схема — критичная граница (MLC-016).
// Эндпоинт пагинирован (BE-17): запрашиваем одну страницу с запасом (pageSize=100) и читаем
// `.items` — UI-листалки нет, но эндпоинт более не материализует всю таблицу.
export function useRecordings() {
  return useQuery({
    queryKey: recordingsQueryKey,
    queryFn: () =>
      api("/api/v1/performance/recordings?page=1&pageSize=100", { schema: recordingsPagedSchema }),
    refetchInterval: 5_000,
    placeholderData: (prev) => prev,
  });
}

// Просмотр одной записи = метаданные + ряд сэмплов. Грузится только когда диалог открыт
// (`enabled` по id). Завершённая запись неизменна, поэтому без polling. Ключ всегда обособлен
// (для id=null — плейсхолдер), чтобы НЕ коллидировать с ключом списка (`recordingsQueryKey`):
// иначе у закрытого диалога `data` подхватил бы массив-список и `data.recording` упал бы.
export function useRecordingDetail(id: string | null) {
  return useQuery({
    queryKey: recordingDetailQueryKey(id ?? "__none__"),
    queryFn: () => api(`/api/v1/performance/recordings/${id}`, { schema: recordingDetailSchema }),
    enabled: id !== null,
  });
}

// Старт записи (Admin). 409 RECORDING_ACTIVE, если запись уже идёт — обрабатывает вызывающий
// через matchConflictCode. Инвалидируем список, чтобы появилась новая активная запись.
export function useStartRecording() {
  return useInvalidatingMutation<RecordingSummary, void>({
    mutationFn: () =>
      api<RecordingSummary>("/api/v1/performance/recordings", {
        method: "POST",
        schema: recordingSummarySchema,
      }),
    invalidate: recordingsQueryKey,
  });
}

// Ручной стоп активной записи (Admin).
export function useStopRecording() {
  return useInvalidatingMutation({
    mutationFn: (id: string) =>
      api<RecordingSummary>(`/api/v1/performance/recordings/${id}/stop`, {
        method: "POST",
        schema: recordingSummarySchema,
      }),
    invalidate: recordingsQueryKey,
  });
}

// Удаление записи (Admin). 409 RECORDING_ACTIVE, если запись ещё идёт (сначала остановить).
export function useDeleteRecording() {
  return useInvalidatingMutation({
    mutationFn: (id: string) =>
      api<null>(`/api/v1/performance/recordings/${id}`, { method: "DELETE" }),
    invalidate: recordingsQueryKey,
  });
}
