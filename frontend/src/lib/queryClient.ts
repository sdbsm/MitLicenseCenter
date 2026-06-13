import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";
import { ApiError, ApiNetworkError, ApiSchemaError } from "./api";
import { markOffline, markOnline } from "./connectionStatus";

// MLC-121 (UX-03/FE-05) — единая классификация ошибок на глобальной границе
// кэшей React Query. Три обособленных канала диагностики (владелец — не
// программист, удалённой телеметрии нет, диагностика идёт через консоль):
//   • ApiNetworkError → глобальный баннер «нет связи» (поднимается markOffline);
//   • ApiSchemaError  → console.error с distinct greppable-префиксом (дрейф BE↔FE);
//   • прочее (ApiError 4xx/5xx) → обрабатывается на месте показа (тост/поле формы).
// Любой успешный запрос снимает баннер «нет связи» (markOnline) — фактический
// успех первичнее, чем navigator.onLine.
export function classifyError(error: unknown): void {
  if (error instanceof ApiNetworkError) {
    markOffline();
    return;
  }
  if (error instanceof ApiSchemaError) {
    // Distinct-префикс для grep'а в консоли: расхождение контракта бэкенда с
    // ожидаемой Zod-схемой FE отделено от сетевых и прочих ошибок.
    console.error("[ApiSchemaError]", error.path, error.issues);
  }
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: classifyError,
    onSuccess: () => markOnline(),
  }),
  mutationCache: new MutationCache({
    onError: classifyError,
    onSuccess: () => markOnline(),
  }),
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          return false;
        }
        return failureCount < 2;
      },
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
    mutations: {
      retry: false,
    },
  },
});
