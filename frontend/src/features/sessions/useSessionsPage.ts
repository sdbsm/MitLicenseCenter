import { useMemo, useState } from "react";
import { useSearchParams } from "react-router";
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import type { SessionSnapshotEntry } from "./types";
import { useSessionsSnapshot } from "./useSessionsSnapshot";

function parseParams(params: URLSearchParams) {
  return {
    q: params.get("q") ?? "",
    infobaseId: params.get("infobaseId") ?? "",
  };
}

/**
 * Оркестрация страницы сессий: данные снапшота/инфобаз, URL-фильтры (поиск + инфобаза),
 * вычисление отфильтрованного набора и состояние диалога принудительного завершения.
 * Презентация (таблица, фильтры) вынесена в отдельные компоненты — поведение 1:1 с прежней
 * монолитной страницей (MLC-032).
 */
export function useSessionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { q, infobaseId } = useMemo(() => parseParams(searchParams), [searchParams]);

  const { data, isLoading, isError, refetch, failureCount } = useSessionsSnapshot();
  const { data: infobasesData } = useInfobases();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  const [selectedSession, setSelectedSession] = useState<SessionSnapshotEntry | null>(null);
  const [killOpen, setKillOpen] = useState(false);

  const infobaseById = useMemo(() => {
    const map = new Map<string, string>();
    for (const ib of infobasesData?.items ?? []) {
      map.set(ib.id, ib.name);
    }
    return map;
  }, [infobasesData]);

  const filtered = useMemo(() => {
    let rows = data?.items ?? [];
    if (q) {
      const lq = q.toLowerCase();
      rows = rows.filter(
        (r) => r.tenantName.toLowerCase().includes(lq) || r.userName.toLowerCase().includes(lq)
      );
    }
    if (infobaseId) {
      const name = infobaseById.get(infobaseId);
      if (name) {
        rows = rows.filter((r) => r.infobaseName === name);
      }
    }
    return rows;
  }, [data, q, infobaseId, infobaseById]);

  const setFilter = (next: { q?: string; infobaseId?: string }) => {
    const params = new URLSearchParams();
    const newQ = next.q !== undefined ? next.q : q;
    const newInfobaseId = next.infobaseId !== undefined ? next.infobaseId : infobaseId;
    if (newQ) params.set("q", newQ);
    if (newInfobaseId) params.set("infobaseId", newInfobaseId);
    setSearchParams(params, { replace: true });
  };

  const handleKillClick = (session: SessionSnapshotEntry) => {
    setSelectedSession(session);
    setKillOpen(true);
  };

  const handleKillOpenChange = (open: boolean) => {
    setKillOpen(open);
    if (!open) setSelectedSession(null);
  };

  return {
    snapshot: data,
    isLoading,
    isError,
    refetch,
    failureCount,
    isAdmin,
    infobases: infobasesData?.items ?? [],
    q,
    infobaseId,
    filtered,
    setFilter,
    selectedSession,
    killOpen,
    handleKillClick,
    handleKillOpenChange,
  };
}
