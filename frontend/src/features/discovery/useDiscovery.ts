import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

// Соответствует backend DiscoveryResponse<T> (DiscoveryEndpoints.cs).
// available=false → источник (кластер/SQL/IIS) недоступен, форма показывает ручной ввод.
export interface DiscoveryResponse<T> {
  items: T[];
  available: boolean;
  error: string | null;
}

export interface ClusterInfobaseDto {
  id: string;
  name: string;
  description: string | null;
}

export interface IisSiteDto {
  siteName: string;
}

export interface PlatformVersionDto {
  version: string;
  architecture: string | null;
}

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
      api<DiscoveryResponse<ClusterInfobaseDto>>("/api/v1/discovery/cluster-infobases"),
    enabled,
    staleTime: STALE_TIME,
  });
}

export function useDatabases(server: string, enabled: boolean) {
  const trimmed = server.trim();
  return useQuery({
    queryKey: ["discovery", "databases", trimmed],
    queryFn: () =>
      api<DiscoveryResponse<string>>(
        `/api/v1/discovery/databases?server=${encodeURIComponent(trimmed)}`
      ),
    enabled: enabled && trimmed.length > 0,
    staleTime: STALE_TIME,
  });
}

export function useIisSites(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "iis-sites"],
    queryFn: () => api<DiscoveryResponse<IisSiteDto>>("/api/v1/discovery/iis-sites"),
    enabled,
    staleTime: STALE_TIME,
  });
}

export function useRacPaths(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "rac-paths"],
    queryFn: () => api<DiscoveryResponse<string>>("/api/v1/discovery/rac-paths"),
    enabled,
    staleTime: STALE_TIME,
  });
}

export function usePlatformVersions(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "platform-versions"],
    queryFn: () =>
      api<DiscoveryResponse<PlatformVersionDto>>("/api/v1/discovery/platform-versions"),
    enabled,
    staleTime: STALE_TIME,
  });
}

// MLC-056: локальные инстансы SQL (из реестра) — пикер сервера БД на /settings и
// в форме инфобазы. Без параметров (localhost-only).
export function useSqlInstances(enabled: boolean) {
  return useQuery({
    queryKey: ["discovery", "sql-instances"],
    queryFn: () => api<DiscoveryResponse<string>>("/api/v1/discovery/sql-instances"),
    enabled,
    staleTime: STALE_TIME,
  });
}
