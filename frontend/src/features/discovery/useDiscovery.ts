import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@/lib/api";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схемы discovery-ответов (MLC-132, FE-09).
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, ADR-32):
 * error=null → ключ отсутствует → omittable(), а не nullable().
 * description у ClusterInfobaseDto и architecture у PlatformVersionDto — nullable.
 *
 * Конверт DiscoveryResponse<T> — generic-фабрика: одна схема конверта
 * переиспользуется всеми discovery-хуками.
 */
function discoveryResponseSchema<T>(item: z.ZodType<T>) {
  return z.object({
    items: z.array(item),
    available: z.boolean(),
    error: omittable(z.string()),
  });
}

export const clusterInfobaseDtoSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: omittable(z.string()),
});

export const iisSiteDtoSchema = z.object({
  siteName: z.string(),
});

export const platformVersionDtoSchema = z.object({
  version: z.string(),
  architecture: omittable(z.string()),
});

export const clusterInfobasesResponseSchema = discoveryResponseSchema(clusterInfobaseDtoSchema);
export const databasesResponseSchema = discoveryResponseSchema(z.string());
export const iisSitesDiscoveryResponseSchema = discoveryResponseSchema(iisSiteDtoSchema);
export const racPathsResponseSchema = discoveryResponseSchema(z.string());
export const platformVersionsResponseSchema = discoveryResponseSchema(platformVersionDtoSchema);
export const sqlInstancesResponseSchema = discoveryResponseSchema(z.string());

// Соответствует backend DiscoveryResponse<T> (DiscoveryEndpoints.cs).
// available=false → источник (кластер/SQL/IIS) недоступен, форма показывает ручной ввод.
export type DiscoveryResponse<T> = { items: T[]; available: boolean; error: string | null };

export type ClusterInfobaseDto = z.infer<typeof clusterInfobaseDtoSchema>;
export type IisSiteDto = z.infer<typeof iisSiteDtoSchema>;
export type PlatformVersionDto = z.infer<typeof platformVersionDtoSchema>;

const STALE_TIME = 5 * 60 * 1000;

// Нормализует состояние react-query в props для DiscoveryField. Чистая функция —
// покрыта Vitest. До загрузки данных считаем источник доступным (покажем загрузку),
// сетевая ошибка или available=false с бэка → недоступен (форма уходит в ручной ввод).
export interface DiscoveryState {
  available: boolean;
  loading: boolean;
  error: string | null;
}

export function toDiscoveryState(query: {
  data?: { available: boolean; error: string | null };
  isError: boolean;
  isFetching: boolean;
}): DiscoveryState {
  const available = !query.isError && (query.data ? query.data.available : true);
  const error = query.data?.error ?? (query.isError ? "request-failed" : null);
  return { available, loading: query.isFetching, error };
}

export function useClusterInfobases(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "cluster-infobases"],
    queryFn: () =>
      api("/api/v1/discovery/cluster-infobases", { schema: clusterInfobasesResponseSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}

// MLC-087 (single-host): сервер берётся из настройки Sql.Server на бекенде, query-параметра
// нет. Незаданная настройка → ответ Available:false (форма уходит в ручной ввод имени БД).
export function useDatabases(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "databases"],
    queryFn: () => api("/api/v1/discovery/databases", { schema: databasesResponseSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}

export function useIisSites(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "iis-sites"],
    queryFn: () => api("/api/v1/discovery/iis-sites", { schema: iisSitesDiscoveryResponseSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}

export function useRacPaths(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "rac-paths"],
    queryFn: () => api("/api/v1/discovery/rac-paths", { schema: racPathsResponseSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}

export function usePlatformVersions(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "platform-versions"],
    queryFn: () =>
      api("/api/v1/discovery/platform-versions", { schema: platformVersionsResponseSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}

// MLC-056: локальные инстансы SQL (из реестра) — пикер сервера БД на /settings и
// в форме инфобазы. Без параметров (localhost-only).
export function useSqlInstances(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "sql-instances"],
    queryFn: () => api("/api/v1/discovery/sql-instances", { schema: sqlInstancesResponseSchema }),
    enabled,
    staleTime: STALE_TIME,
  });
}
