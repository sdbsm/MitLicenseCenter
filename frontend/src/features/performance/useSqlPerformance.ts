import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { sqlPerformanceViewSchema } from "./types";

export const sqlPerformanceQueryKey = ["performance", "sql"] as const;

// Live-снимок нагрузки на MSSQL «1С грузит SQL?» (MLC-068/069, ADR-26, Фаза 3). Pull-по-требованию:
// бэкенд на каждый poll читает DMV (активные запросы + цепочки блокировок + IO-stall + дельта
// wait-stats) и ничего не персистит — сбор идёт, только пока вкладка открыта. 5с согласовано с
// host- и 1С-снимками. placeholderData оставляет прежний снимок между poll'ами (без мигания).
// Схема — критичная граница (MLC-016): perf-поля питают подсветку «кто грузит/заблокирован».
export function useSqlPerformance() {
  return useQuery({
    queryKey: sqlPerformanceQueryKey,
    queryFn: () => api("/api/v1/performance/sql", { schema: sqlPerformanceViewSchema }),
    refetchInterval: 5_000,
    refetchOnWindowFocus: true,
    placeholderData: (prev) => prev,
  });
}
