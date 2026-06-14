import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { infobasesQueryKey } from "@/features/infobases/useInfobases";
import {
  iisAppPoolsResponseSchema,
  iisServerStatusSchema,
  iisSitesResponseSchema,
  type IisAppPoolsResponse,
  type IisOperationResponse,
  type IisServerStatus,
  type IisSitesResponse,
} from "./iisTypes";

// MLC-047 (ADR-24): хуки управления жизненным циклом IIS. Discovery (пулы/сайты) +
// мутации recycle/start/stop/restart/iisreset. После мутации инвалидируем discovery
// (бейджи состояний) и список инфобаз (iisreset/stop меняют статус публикаций в нём).
// MLC-132: read-ответы проходят Zod-валидацию (схемы в iisTypes.ts); enum state
// forward-compatible (незнакомое будущее значение не роняет список).
export const iisServerQueryKey = ["iis", "server"] as const;
export const iisPoolsQueryKey = ["iis", "pools"] as const;
export const iisSitesQueryKey = ["iis", "sites"] as const;

const invalidateIis = () => [
  iisServerQueryKey,
  iisPoolsQueryKey,
  iisSitesQueryKey,
  infobasesQueryKey,
];

// Состояние IIS в целом (служба W3SVC) — для бейджа и кнопки-переключателя stop/start.
export function useIisServerStatus() {
  return useQuery({
    queryKey: iisServerQueryKey,
    queryFn: () => api<IisServerStatus>("/api/v1/iis/server", { schema: iisServerStatusSchema }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}

export function useIisAppPools() {
  return useQuery({
    queryKey: iisPoolsQueryKey,
    queryFn: () =>
      api<IisAppPoolsResponse>("/api/v1/iis/application-pools", {
        schema: iisAppPoolsResponseSchema,
      }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}

export function useIisSites() {
  return useQuery({
    queryKey: iisSitesQueryKey,
    queryFn: () => api<IisSitesResponse>("/api/v1/iis/sites", { schema: iisSitesResponseSchema }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}

// Recycle пула — разрушительная операция: confirm:true обязателен (серверный гейт),
// токен-подтверждение обеспечивает UI перед вызовом.
// Мутации возвращают IisOperationResponse (echo-ответ с именем и новым state);
// значимость схемы здесь ниже (state читается фоновым refetch discovery), поэтому
// валидацию на мутациях пропускаем (see MLC-132 PR body).
export function useRecyclePool() {
  return useInvalidatingMutation({
    mutationFn: (name: string) =>
      api<IisOperationResponse>("/api/v1/iis/application-pools/recycle", {
        method: "POST",
        body: { name, confirm: true },
      }),
    invalidate: invalidateIis,
  });
}

export function useStartPool() {
  return useInvalidatingMutation({
    mutationFn: (name: string) =>
      api<IisOperationResponse>("/api/v1/iis/application-pools/start", {
        method: "POST",
        body: { name },
      }),
    invalidate: invalidateIis,
  });
}

export function useStopPool() {
  return useInvalidatingMutation({
    mutationFn: (name: string) =>
      api<IisOperationResponse>("/api/v1/iis/application-pools/stop", {
        method: "POST",
        body: { name },
      }),
    invalidate: invalidateIis,
  });
}

export function useStartSite() {
  return useInvalidatingMutation({
    mutationFn: (name: string) =>
      api<IisOperationResponse>("/api/v1/iis/sites/start", {
        method: "POST",
        body: { name },
      }),
    invalidate: invalidateIis,
  });
}

export function useStopSite() {
  return useInvalidatingMutation({
    mutationFn: (name: string) =>
      api<IisOperationResponse>("/api/v1/iis/sites/stop", {
        method: "POST",
        body: { name },
      }),
    invalidate: invalidateIis,
  });
}

export function useRestartSite() {
  return useInvalidatingMutation({
    mutationFn: (name: string) =>
      api<IisOperationResponse>("/api/v1/iis/sites/restart", {
        method: "POST",
        body: { name },
      }),
    invalidate: invalidateIis,
  });
}

// Полный iisreset (restart) — confirm:true обязателен (роняет все сайты сервера).
export function useResetIis() {
  return useInvalidatingMutation<void, void>({
    mutationFn: () =>
      api<void>("/api/v1/iis/reset", {
        method: "POST",
        body: { confirm: true },
      }),
    invalidate: invalidateIis,
  });
}

// iisreset /stop — confirm:true обязателен (останавливает весь IIS).
export function useStopIis() {
  return useInvalidatingMutation<void, void>({
    mutationFn: () =>
      api<void>("/api/v1/iis/stop", {
        method: "POST",
        body: { confirm: true },
      }),
    invalidate: invalidateIis,
  });
}

// iisreset /start — восстановление, без confirm.
export function useStartIis() {
  return useInvalidatingMutation<void, void>({
    mutationFn: () => api<void>("/api/v1/iis/start", { method: "POST" }),
    invalidate: invalidateIis,
  });
}
