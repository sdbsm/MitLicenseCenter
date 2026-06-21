import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import {
  investigationDetailSchema,
  investigationsPagedSchema,
  investigationSummarySchema,
  progressSchema,
  reportSchema,
  type InvestigationSummary,
  type StartInvestigationRequest,
} from "./types";

// react-query хуки раздела «Расследование» (MLC-239, трек 1.2). Зеркаль useRecordings (раздел «Запись»):
// чтение = Viewer, мутации (старт/стоп/удаление) = Admin. Схемы — критичная граница (MLC-016). Экраны —
// этап D (MLC-241+); хуки заведены здесь как контракт-слой под будущие экраны.
export const investigationsQueryKey = ["investigations"] as const;
export const investigationDetailQueryKey = (id: string) => ["investigations", id] as const;
export const investigationReportQueryKey = (id: string) =>
  ["investigations", id, "report"] as const;
export const investigationProgressQueryKey = (id: string) =>
  ["investigations", id, "progress"] as const;

// Список дел (Viewer). Поллим 5с, пока идёт активное дело — индикатор и счётчик находок свежие (в ритме
// раздела). Дел немного, persisted-список дёшев. Эндпоинт пагинирован — берём одну страницу с запасом.
export function useInvestigations() {
  return useQuery({
    queryKey: investigationsQueryKey,
    queryFn: () =>
      api("/api/v1/investigations?page=1&pageSize=100", { schema: investigationsPagedSchema }),
    refetchInterval: 5_000,
    placeholderData: (prev) => prev,
  });
}

// Деталь дела = шапка + снимок сбора + находки. Грузится по id (enabled). Завершённое дело неизменно —
// без polling. Ключ обособлен от списка (для id=null — плейсхолдер), чтобы не коллидировать.
export function useInvestigationDetail(id: string | null) {
  return useQuery({
    queryKey: investigationDetailQueryKey(id ?? "__none__"),
    queryFn: () => api(`/api/v1/investigations/${id}`, { schema: investigationDetailSchema }),
    enabled: id !== null,
  });
}

// Отчёт по делу (Viewer): ранжированные находки + текстовые рекомендации (вычисляется на бэкенде).
export function useInvestigationReport(id: string | null) {
  return useQuery({
    queryKey: investigationReportQueryKey(id ?? "__none__"),
    queryFn: () => api(`/api/v1/investigations/${id}/report`, { schema: reportSchema }),
    enabled: id !== null,
  });
}

// Лёгкий прогресс активного дела (Viewer): статус + прошедшее время + размер собранного. Поллим 5с,
// пока окно прогресса открыто (enabled по id). Дёшево, без тяжёлых JOIN.
export function useInvestigationProgress(id: string | null) {
  return useQuery({
    queryKey: investigationProgressQueryKey(id ?? "__none__"),
    queryFn: () => api(`/api/v1/investigations/${id}/progress`, { schema: progressSchema }),
    enabled: id !== null,
    refetchInterval: 5_000,
  });
}

// Старт расследования (Admin). 409 INVESTIGATION_ACTIVE, если дело уже идёт; INVESTIGATION_START_FAILED —
// нет прав/места/корень 1С (detail несёт причину + icacls). Инвалидируем список — появится новое дело.
export function useStartInvestigation() {
  return useInvalidatingMutation<InvestigationSummary, StartInvestigationRequest>({
    mutationFn: (body) =>
      api<InvestigationSummary>("/api/v1/investigations", {
        method: "POST",
        body,
        schema: investigationSummarySchema,
      }),
    invalidate: investigationsQueryKey,
  });
}

// Ручной стоп активного дела (Admin). 409 INVESTIGATION_NOT_ACTIVE, если дело не текущее активное.
export function useStopInvestigation() {
  return useInvalidatingMutation({
    mutationFn: (id: string) =>
      api<InvestigationSummary>(`/api/v1/investigations/${id}/stop`, {
        method: "POST",
        schema: investigationSummarySchema,
      }),
    invalidate: investigationsQueryKey,
  });
}

// Удаление дела (Admin). 409 INVESTIGATION_ACTIVE, если дело ещё активно (сначала остановить).
export function useDeleteInvestigation() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/investigations/${id}`, { method: "DELETE" }),
    invalidate: investigationsQueryKey,
  });
}
