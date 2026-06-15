import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { api } from "@/lib/api";
import {
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type PaginationState,
  type SortingState,
} from "@tanstack/react-table";
import { useTableDensity } from "@/components/ui/data-table";
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import { buildSessionColumns } from "./sessionColumns";
import type { SessionSnapshotEntry } from "./types";
import { useSessionsSnapshot } from "./useSessionsSnapshot";

export type SessionSortKey = keyof Pick<
  SessionSnapshotEntry,
  "tenantName" | "infobaseName" | "userName" | "startedAt" | "durationSeconds" | "licenseStatus"
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

/**
 * @internal — экспортируется только для теста клиентской сортировки (clientSortPagination).
 * Канонический компаратор сеансов (UX-14); те же правила применяются как `sortingFn`
 * колонок в `sessionColumns` (DataTable, MLC-144).
 */
// ADR-48 (MLC-166): порядок трёхсостояния licenseStatus при asc —
// Consuming → NotConsuming → Pending (считается → не считается → определяется).
const licenseStatusOrder: Record<SessionSnapshotEntry["licenseStatus"], number> = {
  Consuming: 0,
  NotConsuming: 1,
  Pending: 2,
};

export function sortRows(rows: SessionSnapshotEntry[], sort: SessionSort): SessionSnapshotEntry[] {
  return [...rows].sort((a, b) => {
    const av = a[sort.key];
    const bv = b[sort.key];
    let cmp: number;
    if (sort.key === "licenseStatus") {
      cmp =
        licenseStatusOrder[av as SessionSnapshotEntry["licenseStatus"]] -
        licenseStatusOrder[bv as SessionSnapshotEntry["licenseStatus"]];
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
 * сборка экземпляра `useReactTable` с клиентской сортировкой (`getSortedRowModel`) и
 * клиентской пагинацией (`getPaginationRowModel`, size=25) над живым снапшотом (UX-14).
 * URL-фильтры q/infobaseId сохранены без смены поведения (MLC-144). Презентация — DataTable
 * в `SessionsTable` (MLC-032).
 */
export function useSessionsPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { q, infobaseId } = useMemo(() => parseParams(searchParams), [searchParams]);

  // MLC-156: пауза авто-обновления + ручной форс-обход. При паузе refetchInterval=false.
  const [isPaused, setIsPaused] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const togglePause = useCallback(() => setIsPaused((p) => !p), []);

  const { data, isLoading, isError, refetch, failureCount } = useSessionsSnapshot(isPaused);
  const { data: infobasesData } = useInfobases();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  // «Обновить сейчас» = живой форс-обход 1С: POST /sessions/refresh запускает cold-прогон
  // прямо сейчас и ждёт его завершения, затем перечитываем свежий снимок. Работает и на
  // паузе (ручное обновление). 204 без тела — schema не нужна.
  const refreshNow = useCallback(async () => {
    setIsRefreshing(true);
    try {
      await api("/api/v1/sessions/refresh", { method: "POST" });
      await refetch();
    } finally {
      setIsRefreshing(false);
    }
  }, [refetch]);

  const { density, toggleDensity } = useTableDensity();

  const [selectedSession, setSelectedSession] = useState<SessionSnapshotEntry | null>(null);
  const [killOpen, setKillOpen] = useState(false);
  const [sorting, setSorting] = useState<SortingState>([{ id: "startedAt", desc: true }]);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: SESSIONS_PAGE_SIZE,
  });

  const infobaseById = useMemo(() => {
    const map = new Map<string, string>();
    for (const ib of infobasesData?.items ?? []) {
      map.set(ib.id, ib.name);
    }
    return map;
  }, [infobasesData]);

  // Фильтрация q/infobaseId — кросс-колоночная, остаётся вне tanstack columnFilters
  // (как раньше, чтобы не менять имена URL-параметров).
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
      if (name) rows = rows.filter((r) => r.infobaseName === name);
    }
    return rows;
  }, [data, q, infobaseId, infobaseById]);

  const handleKillClick = (session: SessionSnapshotEntry) => {
    setSelectedSession(session);
    setKillOpen(true);
  };
  const handleKillOpenChange = (open: boolean) => {
    setKillOpen(open);
    if (!open) setSelectedSession(null);
  };

  const columns = useMemo(
    () => buildSessionColumns({ t, isAdmin, onKill: handleKillClick }),
    [t, isAdmin]
  );

  const table = useReactTable({
    data: filtered,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    state: { sorting, pagination },
    onSortingChange: setSorting,
    onPaginationChange: setPagination,
    autoResetPageIndex: false,
  });

  // Clamp страницы в [0, pageCount-1] при изменении длины снапшота (live-данные, UX-14).
  const pageCount = table.getPageCount();
  useEffect(() => {
    if (pagination.pageIndex > 0 && pagination.pageIndex > pageCount - 1) {
      setPagination((p) => ({ ...p, pageIndex: Math.max(0, pageCount - 1) }));
    }
  }, [pageCount, pagination.pageIndex]);

  // Любая смена сортировки/фильтра возвращает на первую страницу.
  const setFilter = (next: { q?: string; infobaseId?: string }) => {
    const params = new URLSearchParams();
    const newQ = next.q !== undefined ? next.q : q;
    const newInfobaseId = next.infobaseId !== undefined ? next.infobaseId : infobaseId;
    if (newQ) params.set("q", newQ);
    if (newInfobaseId) params.set("infobaseId", newInfobaseId);
    setSearchParams(params, { replace: true });
    setPagination((p) => ({ ...p, pageIndex: 0 }));
  };
  useEffect(() => {
    setPagination((p) => (p.pageIndex === 0 ? p : { ...p, pageIndex: 0 }));
  }, [sorting]);

  return {
    snapshot: data,
    isLoading,
    isError,
    refetch,
    failureCount,
    isPaused,
    togglePause,
    refreshNow,
    isRefreshing,
    isAdmin,
    infobases: infobasesData?.items ?? [],
    q,
    infobaseId,
    filtered,
    table,
    density,
    toggleDensity,
    // Пагинация для PaginationBar (1-based для UI).
    page: pagination.pageIndex + 1,
    totalFiltered: filtered.length,
    setPage: (page1: number) => setPagination((p) => ({ ...p, pageIndex: page1 - 1 })),
    setFilter,
    selectedSession,
    killOpen,
    handleKillClick,
    handleKillOpenChange,
  };
}
