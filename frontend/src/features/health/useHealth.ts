import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { healthSchema } from "./types";

export const healthQueryKey = ["health"] as const;

// MLC-149: версия установленной панели для подвала сайдбара. Анонимный liveness
// `/api/v1/health` дёшев и не требует аутентификации (отдаёт версию ещё до логина).
// Ответ проходит толерантную Zod-границу (по образцу useDashboardSummary).
//
// Версия не меняется в рантайме процесса, поэтому без авто-рефетча: тянем один раз,
// держим бесконечно (staleTime: Infinity). Один ретрай на случай гонки со стартом.
// При недоступном /health `data` остаётся undefined — потребитель скрывает строку.
export function useHealth() {
  return useQuery({
    queryKey: healthQueryKey,
    queryFn: () => api("/api/v1/health", { schema: healthSchema }),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: 1,
  });
}
