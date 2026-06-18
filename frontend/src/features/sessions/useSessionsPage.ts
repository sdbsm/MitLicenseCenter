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
import { useDashboardSummary } from "@/features/dashboard/useDashboardSummary";
import { useAllTenants } from "@/features/tenants/useTenants";
import { buildConsumedByTenant } from "@/features/tenants/useTenantConsumption";
import { buildByTenantRows } from "./byTenantRows";
import { buildSessionColumns } from "./sessionColumns";
import { appTypeLabel, isInteractiveAppId, KNOWN_APP_IDS } from "./appTypes";
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

/**
 * Проекции страницы «Сеансы» (MLC-196a, Фаза 1): дом темы лицензий с двумя видами.
 * `byTenant` (дефолт) — агрегат «кто сколько потребляет»; `live` — текущий live-снимок.
 */
export type SessionsView = "byTenant" | "live";

/** URL → активная проекция. Любое нераспознанное значение → дефолт `byTenant`. */
export function parseView(params: URLSearchParams): SessionsView {
  return params.get("view") === "live" ? "live" : "byTenant";
}

/**
 * URL → состояние фильтров. `appIds` — CSV; различаем «параметр отсутствует» (`null` →
 * дефолт: только интерактивные типы) и «явно задан» (массив, в т.ч. пустой → показать
 * ровно выбранное, пустой = ничего). См. `effectiveAppIds`.
 */
export function parseParams(params: URLSearchParams) {
  const rawAppIds = params.get("appIds");
  return {
    q: params.get("q") ?? "",
    infobaseId: params.get("infobaseId") ?? "",
    appIds:
      rawAppIds === null
        ? null
        : rawAppIds
            .split(",")
            .map((s) => s.trim())
            .filter(Boolean),
    // MLC-167: тумблер «Только лицензионные». ВКЛ по умолчанию — отсутствие параметра
    // означает «показывать только Consuming». `consuming=0` явно выключает режим.
    consuming: params.get("consuming") !== "0",
  };
}

/**
 * Дефолт фильтра типов сеансов (MLC-165): когда URL-параметр `appIds` отсутствует,
 * показываем только интерактивные типы, реально присутствующие в снапшоте (пересечение
 * INTERACTIVE_APP_IDS ∩ present). Это и визуальный выбор в селекте, и набор для фильтрации.
 */
export function defaultAppIds(presentAppIds: string[]): string[] {
  return presentAppIds.filter(isInteractiveAppId);
}

/**
 * Эффективный выбор типов: явный (из URL, в т.ч. пустой = «ничего») либо дефолт
 * (интерактивные ∩ присутствующие). @internal — вынесено для теста.
 */
export function resolveAppIds(appIds: string[] | null, presentAppIds: string[]): string[] {
  return appIds === null ? defaultAppIds(presentAppIds) : appIds;
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
  const { q, infobaseId, appIds, consuming } = useMemo(
    () => parseParams(searchParams),
    [searchParams]
  );
  const view = useMemo(() => parseView(searchParams), [searchParams]);

  // MLC-156: пауза авто-обновления + ручной форс-обход. При паузе refetchInterval=false.
  const [isPaused, setIsPaused] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const togglePause = useCallback(() => setIsPaused((p) => !p), []);

  const { data, isLoading, isError, refetch, failureCount } = useSessionsSnapshot(isPaused);
  const { data: infobasesData } = useInfobases();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  // Проекция «По клиентам» (MLC-196a): клиентская склейка БЕЗ нового BE-эндпоинта —
  // все клиенты (имя + лимит) из useAllTenants × потребление из того же снапшота сеансов,
  // что уже грузит страница (buildConsumedByTenant — без второго параллельного запроса).
  const { data: tenantsData, isLoading: tenantsLoading } = useAllTenants();
  const byTenantRows = useMemo(() => {
    const consumed = buildConsumedByTenant(data?.items ?? []);
    return buildByTenantRows(tenantsData?.items ?? [], consumed);
  }, [tenantsData, data]);

  // Лицензионный банд проекции «Живые сеансы» (MLC-196a): host-уровень из /dashboard/summary
  // (без нового контракта). Свободно = доступно по сводке (licensesAvailableTotal).
  const { data: summary, isLoading: summaryLoading } = useDashboardSummary();
  const licenseBand = useMemo(
    () => ({
      consumed: summary?.licensesConsumedTotal ?? 0,
      limit: (summary?.licensesConsumedTotal ?? 0) + (summary?.licensesAvailableTotal ?? 0),
      free: summary?.licensesAvailableTotal ?? 0,
      active: summary?.sessionsActiveTotal ?? 0,
    }),
    [summary]
  );

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

  // app-id, реально присутствующие в текущем снапшоте (для опций фильтра и дефолта).
  const presentAppIds = useMemo(() => {
    const set = new Set<string>();
    for (const item of data?.items ?? []) set.add(item.appId);
    return [...set];
  }, [data]);

  // Эффективный выбор типов: явный из URL (в т.ч. пустой = «ничего») либо дефолт
  // (интерактивные ∩ присутствующие). Используется и для фильтрации, и как visual-выбор
  // в селекте, поэтому экспортируется в SessionsFiltersBar (MLC-165).
  const effectiveAppIds = useMemo(
    () => resolveAppIds(appIds, presentAppIds),
    [appIds, presentAppIds]
  );

  // Опции селекта «Тип сеанса» (MLC-167): полный каталог KNOWN_APP_IDS (в каноническом
  // порядке) ∪ присутствующие в снапшоте типы, которых нет в каталоге (незнакомые app-id
  // из кластера — в конец). Полный каталог даёт возможность заранее отметить тип, которого
  // ещё нет онлайн. Человеческое имя — из маппинга (или сам app-id, если типа нет).
  const appTypeOptions = useMemo(() => {
    const known = new Set<string>(KNOWN_APP_IDS);
    const extras = presentAppIds
      .filter((appId) => !known.has(appId))
      .sort((a, b) => a.localeCompare(b, "ru"));
    return [...KNOWN_APP_IDS, ...extras].map((appId) => ({
      value: appId,
      label: appTypeLabel(t, appId),
    }));
  }, [presentAppIds, t]);

  // Фильтрация q/infobaseId/appIds — кросс-колоночная, остаётся вне tanstack columnFilters
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
    // MLC-167: тумблер «Только лицензионные» перекрывает фильтр типов. ВКЛ (дефолт) →
    // показываем только фактически потребляющие лицензию (licenseStatus === "Consuming");
    // Pending/NotConsuming скрыты, фильтр типов игнорируется. ВЫКЛ → действует фильтр типов.
    if (consuming) {
      rows = rows.filter((r) => r.licenseStatus === "Consuming");
    } else {
      // Явный пустой выбор (`appIds === []`) трактуем как «показать пусто» — это валидное
      // состояние «оператор снял все типы». Дефолт (appIds===null) — интерактивные.
      const appIdSet = new Set(effectiveAppIds);
      rows = rows.filter((r) => appIdSet.has(r.appId));
    }
    return rows;
  }, [data, q, infobaseId, infobaseById, effectiveAppIds, consuming]);

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
  const setFilter = (next: {
    q?: string;
    infobaseId?: string;
    appIds?: string[];
    consuming?: boolean;
    view?: SessionsView;
  }) => {
    const params = new URLSearchParams();
    // view сохраняется при перестройке URL (дефолт byTenant не пишем — чистый URL).
    const newView = next.view !== undefined ? next.view : view;
    if (newView !== "byTenant") params.set("view", newView);
    const newQ = next.q !== undefined ? next.q : q;
    const newInfobaseId = next.infobaseId !== undefined ? next.infobaseId : infobaseId;
    if (newQ) params.set("q", newQ);
    if (newInfobaseId) params.set("infobaseId", newInfobaseId);
    // appIds: как только оператор тронул фильтр типов — он становится «явным» в URL
    // (даже пустой → `appIds=`, что означает «показать пусто», а не вернуть дефолт).
    // Если в этом вызове не трогали — сохраняем текущее URL-состояние (явное или дефолт).
    const newAppIds = next.appIds !== undefined ? next.appIds : appIds;
    if (newAppIds !== null) params.set("appIds", newAppIds.join(","));
    // MLC-167: consuming пишем в URL только когда ВЫКЛ (`consuming=0`), чтобы дефолтный
    // URL (тумблер вкл) оставался чистым. ВКЛ = отсутствие параметра.
    const newConsuming = next.consuming !== undefined ? next.consuming : consuming;
    if (!newConsuming) params.set("consuming", "0");
    setSearchParams(params, { replace: true });
    setPagination((p) => ({ ...p, pageIndex: 0 }));
  };
  useEffect(() => {
    setPagination((p) => (p.pageIndex === 0 ? p : { ...p, pageIndex: 0 }));
  }, [sorting]);

  // Переключатель проекций: меняет только view, остальные параметры сохраняет.
  const setView = (next: SessionsView) => setFilter({ view: next });

  // Клик по строке клиента (проекция «По клиентам») → «Живые сеансы» с фильтром по
  // имени клиента (существующий фильтр q ищет подстроку по tenantName/userName).
  const goToLiveWithTenant = (tenantName: string) => setFilter({ view: "live", q: tenantName });

  return {
    snapshot: data,
    view,
    setView,
    byTenantRows,
    tenantsLoading,
    licenseBand,
    summaryLoading,
    goToLiveWithTenant,
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
    consuming,
    appTypeOptions,
    selectedAppIds: effectiveAppIds,
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
