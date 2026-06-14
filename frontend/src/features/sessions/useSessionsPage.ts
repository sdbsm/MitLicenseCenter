import { useMemo, useState } from "react";
import { useSearchParams } from "react-router";
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import type { SessionSnapshotEntry } from "./types";
import { useSessionsSnapshot } from "./useSessionsSnapshot";

export type SessionSortKey = keyof Pick<
  SessionSnapshotEntry,
  "tenantName" | "infobaseName" | "userName" | "startedAt" | "durationSeconds" | "consumesLicense"
>;

export type SortDir = "asc" | "desc";

export interface SessionSort {
  key: SessionSortKey;
  dir: SortDir;
}

export const SESSIONS_PAGE_SIZE = 25;

function parseParams(params: URLSearchParams) {
  return {
    q: params.get("q") ?? "",
    infobaseId: params.get("infobaseId") ?? "",
  };
}

/** @internal — экспортируется только для тестов */
export function sortRows(rows: SessionSnapshotEntry[], sort: SessionSort): SessionSnapshotEntry[] {
  return [...rows].sort((a, b) => {
    const av = a[sort.key];
    const bv = b[sort.key];
    let cmp: number;
    if (typeof av === "boolean" && typeof bv === "boolean") {
      // true (считается) выше false при asc
      cmp = av === bv ? 0 : av ? -1 : 1;
    } else if (typeof av === "number" && typeof bv === "number") {
      cmp = av - bv;
    } else {
      cmp = String(av).localeCompare(String(bv), "ru");
    }
    return sort.dir === "asc" ? cmp : -cmp;
  });
}

/**
 * Оркестрация страницы сессий: данные снапшота/инфобаз, URL-фильтры (поиск + инфобаза),
 * вычисление отфильтрованного/отсортированного/постраничного набора и состояние диалога
 * принудительного завершения. Сортировка и пагинация — клиентские, над живым снапшотом
 * (UX-14): при изменении длины снапшота страница clamp'ится в [1, totalPages].
 * Презентация (таблица, фильтры) вынесена в отдельные компоненты (MLC-032).
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
  const [sort, setSort] = useState<SessionSort>({ key: "startedAt", dir: "desc" });
  const [page, setPage] = useState(1);

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

  const sorted = useMemo(() => sortRows(filtered, sort), [filtered, sort]);

  // Clamp страницы в [1, totalPages] при изменении длины данных (live-снапшот)
  const totalPages = Math.max(1, Math.ceil(sorted.length / SESSIONS_PAGE_SIZE));
  const safePage = Math.min(page, totalPages);

  const pageRows = useMemo(() => {
    const start = (safePage - 1) * SESSIONS_PAGE_SIZE;
    return sorted.slice(start, start + SESSIONS_PAGE_SIZE);
  }, [sorted, safePage]);

  const toggleSort = (key: SessionSortKey) => {
    setSort((prev) =>
      prev.key === key ? { key, dir: prev.dir === "asc" ? "desc" : "asc" } : { key, dir: "asc" }
    );
    setPage(1);
  };

  const setFilter = (next: { q?: string; infobaseId?: string }) => {
    const params = new URLSearchParams();
    const newQ = next.q !== undefined ? next.q : q;
    const newInfobaseId = next.infobaseId !== undefined ? next.infobaseId : infobaseId;
    if (newQ) params.set("q", newQ);
    if (newInfobaseId) params.set("infobaseId", newInfobaseId);
    setSearchParams(params, { replace: true });
    setPage(1);
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
    sort,
    toggleSort,
    page: safePage,
    totalPages,
    totalFiltered: sorted.length,
    pageRows,
    setPage,
    setFilter,
    selectedSession,
    killOpen,
    handleKillClick,
    handleKillOpenChange,
  };
}
