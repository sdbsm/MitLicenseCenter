import { useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import {
  unassignedInfobasesResponseSchema,
  type HideUnassignedInfobaseInput,
  type UnassignedInfobasesResponse,
} from "./types";

// Ключ — под префиксом ["infobases"], поэтому инвалидация списка инфобаз
// (create/update/delete в useInfobases) prefix-матчем задевает и нераспределённые:
// заведённая база сразу уходит из списка разбора. hide/unhide инвалидируют ровно этот ключ.
export const unassignedInfobasesQueryKey = ["infobases", "unassigned"] as const;

// MLC-093 — нераспределённые базы кластера. Серверный TTL-кэш (60 c, MLC-092) — главный
// источник свежести, поэтому FE держит малый staleTime и доверяет бекенду. Кнопка «Обновить»
// бьёт мимо кэша через `?refresh=true`: флаг живёт в ref и сбрасывается после первого опроса,
// чтобы фоновые инвалидации (после create/hide/unhide) шли по кэшу, а не дёргали RAS.
export function useUnassignedInfobases(enabled: boolean) {
  const refreshRef = useRef(false);

  const query = useQuery({
    queryKey: unassignedInfobasesQueryKey,
    queryFn: () => {
      const refresh = refreshRef.current;
      refreshRef.current = false;
      return api<UnassignedInfobasesResponse>(
        `/api/v1/infobases/unassigned${refresh ? "?refresh=true" : ""}`,
        { schema: unassignedInfobasesResponseSchema }
      );
    },
    enabled,
    staleTime: 10 * 1000,
  });

  // Принудительный опрос RAS мимо серверного кэша (кнопка «Обновить»).
  const refresh = () => {
    refreshRef.current = true;
    return query.refetch();
  };

  return { ...query, refresh };
}

export function useHideUnassignedInfobase() {
  return useInvalidatingMutation({
    mutationFn: ({ clusterInfobaseId, name }: HideUnassignedInfobaseInput) =>
      api<null>(`/api/v1/infobases/unassigned/${clusterInfobaseId}/hide`, {
        method: "POST",
        body: { name },
      }),
    invalidate: unassignedInfobasesQueryKey,
  });
}

export function useUnhideUnassignedInfobase() {
  return useInvalidatingMutation({
    mutationFn: (clusterInfobaseId: string) =>
      api<null>(`/api/v1/infobases/unassigned/${clusterInfobaseId}/hide`, {
        method: "DELETE",
      }),
    invalidate: unassignedInfobasesQueryKey,
  });
}
