import { getCoreRowModel, useReactTable } from "@tanstack/react-table";
import { AlertTriangleIcon, DatabaseIcon, PlusIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { DataTable, useTableDensity } from "@/components/ui/data-table";
import { Input } from "@/components/ui/input";
import { PaginationBar } from "@/components/PaginationBar";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/SearchableSelect";
import { Skeleton } from "@/components/ui/skeleton";
import { TableCell } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { TooltipProvider } from "@/components/ui/tooltip";
import { useMe } from "@/features/auth/useAuth";
import { BackupsDialog } from "@/features/backups/BackupsDialog";
import { BulkChangePlatformDialog } from "@/features/publications/BulkChangePlatformDialog";
import { BulkCheckDialog } from "@/features/publications/BulkCheckDialog";
import { BulkDeleteInfobaseDialog } from "@/features/publications/BulkDeleteInfobaseDialog";
import { BulkPublishDialog } from "@/features/publications/BulkPublishDialog";
import { BulkUnpublishDialog } from "@/features/publications/BulkUnpublishDialog";
import { ChangePlatformDialog } from "@/features/publications/ChangePlatformDialog";
import { IisManagementCard } from "@/features/publications/iis/IisManagementCard";
import { PublicationsBulkBar } from "@/features/publications/PublicationsBulkBar";
import { PublishPublicationDialog } from "@/features/publications/PublishPublicationDialog";
import { UnpublishPublicationDialog } from "@/features/publications/UnpublishPublicationDialog";
import {
  toPublicationListItem,
  type PublicationListItem,
  type PublicationPublishStatus,
} from "@/features/publications/types";
import { useCheckStatus } from "@/features/publications/usePublications";
import type { BulkItemState } from "@/features/publications/useBulkOperation";
import { useAllTenants } from "@/features/tenants/useTenants";
import { DeleteInfobaseDialog, type DeletableInfobase } from "./DeleteInfobaseDialog";
import { buildInfobaseColumns } from "./infobaseColumns";
import { InfobaseFormDialog } from "./InfobaseFormDialog";
import { ReassignInfobaseDialog } from "./ReassignInfobaseDialog";
import type { InfobaseListItem } from "./types";
import type { InfobaseFormPrefill } from "./useInfobaseForm";
import { fetchInfobaseBulkIds, INFOBASES_PAGE_SIZE, useInfobases } from "./useInfobases";
import { MissingInfobasesBanner } from "./unassigned/MissingInfobasesBanner";
import { MissingInfobasesDialog } from "./unassigned/MissingInfobasesDialog";
import { UnassignedBanner } from "./unassigned/UnassignedBanner";
import { UnassignedInfobasesDialog } from "./unassigned/UnassignedInfobasesDialog";
import { useUnassignedInfobases } from "./unassigned/useUnassignedInfobases";
import type { MissingInfobase, UnassignedInfobaseItem } from "./unassigned/types";

const PAGE_SIZE = INFOBASES_PAGE_SIZE;
const ALL_TENANTS = "__all__";
const ALL_STATUSES = "__all__";

// Порядок опций фильтра «Статус публикации» (MLC-090). Ориентир ценности —
// быстро найти «всё, что не Published»: проблемные статусы идут первыми.
const PUBLISH_STATUS_FILTERS: PublicationPublishStatus[] = [
  "NotPublished",
  "Error",
  "Unknown",
  "Published",
];

/**
 * Единая страница «Базы» (MLC-081): таблица инфобаз, обогащённая публикационными
 * колонками и операциями (бывшая страница «Публикации» влита сюда), плюс вкладка
 * «IIS» с управлением пулами/сайтами/iisreset (MLC-047). Grouped-режим «По клиенту»
 * снят (MLC-085, аудит §3.2): сгруппированный взгляд — паспорт клиента /tenants/:id,
 * здесь — flat-список с фильтром по клиенту (?tenantId= — ссылки извне).
 *
 * Таблица — на общем `DataTable` (MLC-144b, ADR-46): серверная пагинация
 * (`manualPagination` + `pageCount`), URL-фильтры клиента/статуса в слоте тулбара,
 * меню видимости колонок и density из тулбара. Bulk-выбор живёт во внешнем
 * `Map<id, PublicationListItem>` (переживает листание и смену фильтра), а не в
 * tanstack row-selection.
 */
export function InfobasesPage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { density, toggleDensity } = useTableDensity();

  const { data: tenantsData } = useAllTenants();
  const tenants = useMemo(() => tenantsData?.items ?? [], [tenantsData]);
  const tenantNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const tenant of tenants) {
      map.set(tenant.id, tenant.name);
    }
    return map;
  }, [tenants]);

  // Фильтры живут в URL — на отфильтрованный список ведут ссылки извне (колонка
  // «Базы» в таблице клиентов, MLC-085), а статус публикации (?publishStatus=, MLC-090)
  // держим там же для консистентности и шаринга ссылкой.
  const [searchParams, setSearchParams] = useSearchParams();
  const tenantFilter = searchParams.get("tenantId") ?? ALL_TENANTS;
  const tenantIdParam = tenantFilter === ALL_TENANTS ? null : tenantFilter;
  const statusFilter = searchParams.get("publishStatus") ?? ALL_STATUSES;
  const publishStatusParam = statusFilter === ALL_STATUSES ? null : statusFilter;
  // MLC-150 — серверный фильтр «не найдена в кластере» (обратный дрейф). Только для
  // админа: при недоступном RAS показываем честное состояние, а не «0 найдено».
  const notInClusterFilter = isAdmin && searchParams.get("notInCluster") === "true";
  // MLC-181a — серверный текстовый поиск по имени базы / имени БД. Терм живёт в URL
  // (?search=) рядом с остальными фильтрами — отфильтрованный список шарится ссылкой.
  const searchParam = searchParams.get("search") ?? "";

  const [page, setPage] = useState(1);

  // MLC-181a: черновик в инпуте, коммит — после debounce в URL-параметр; при смене терма
  // возвращаемся на первую страницу (иначе вторая может оказаться пустой). Образец — TenantsPage.
  const [searchDraft, setSearchDraft] = useState(searchParam);
  // Внешнее изменение URL (назад/вперёд, шаринг ссылки, сброс фильтров) подтягиваем в инпут.
  useEffect(() => {
    setSearchDraft(searchParam);
  }, [searchParam]);
  useEffect(() => {
    const id = setTimeout(() => {
      const next = searchDraft.trim();
      if (next === searchParam) return;
      setSearchParams(
        (prev) => {
          const nextParams = new URLSearchParams(prev);
          if (next) nextParams.set("search", next);
          else nextParams.delete("search");
          return nextParams;
        },
        { replace: true }
      );
      setPage(1);
    }, 300);
    return () => clearTimeout(id);
    // searchParam исключён намеренно: дебаунс реагирует на ввод, не на синхронизацию из URL.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchDraft]);

  const { data, isLoading, isError, isFetching, refetch } = useInfobases(
    tenantIdParam,
    publishStatusParam,
    notInClusterFilter,
    page,
    PAGE_SIZE,
    searchParam
  );
  // BE заполняет clusterAvailable только при notInCluster=true; false ⇒ RAS недоступен,
  // фильтрация не выполнена — показываем «не удалось проверить кластер» вместо пустого списка.
  const clusterUnavailable = notInClusterFilter && data?.clusterAvailable === false;

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<InfobaseListItem | null>(null);
  const [prefill, setPrefill] = useState<InfobaseFormPrefill | null>(null);
  const [unassignedOpen, setUnassignedOpen] = useState(false);
  const [missingOpen, setMissingOpen] = useState(false);
  const [deleting, setDeleting] = useState<DeletableInfobase | null>(null);
  const [reassigning, setReassigning] = useState<InfobaseListItem | null>(null);
  const [backupsFor, setBackupsFor] = useState<InfobaseListItem | null>(null);

  // MLC-081: операции с публикацией строки (бывшая страница «Публикации»).
  const [checkingId, setCheckingId] = useState<string | null>(null);
  const [publishTarget, setPublishTarget] = useState<PublicationListItem | null>(null);
  const [unpublishTarget, setUnpublishTarget] = useState<PublicationListItem | null>(null);
  const [platformTarget, setPlatformTarget] = useState<PublicationListItem | null>(null);
  const checkStatus = useCheckStatus();

  // MLC-046/081: множественный выбор для bulk-операций. Список серверно пагинирован,
  // поэтому храним сами объекты (id публикации → строка): выбор переживает листание
  // страниц и смену фильтра, объекты со «спрятанных» страниц остаются доступны диалогам.
  const [selected, setSelected] = useState<Map<string, PublicationListItem>>(new Map());
  const [bulkPublishOpen, setBulkPublishOpen] = useState(false);
  const [bulkPlatformOpen, setBulkPlatformOpen] = useState(false);
  // MLC-184b — bulk-действия из меню «Ещё» в баре (проверка / снятие с публикации / удаление баз).
  const [bulkCheckOpen, setBulkCheckOpen] = useState(false);
  const [bulkUnpublishOpen, setBulkUnpublishOpen] = useState(false);
  const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
  // MLC-181c — идёт запрос /infobases/ids для «Выбрать все N по фильтру» (кнопка disabled).
  const [selectingAllFiltered, setSelectingAllFiltered] = useState(false);
  // Выбор наполнен через «Выбрать все по фильтру» (а не поэлементно/постранично) — тогда
  // bulk-диалог показывает сводку активного фильтра, чтобы оператор видел, к чему применяет.
  const [selectionFromFilter, setSelectionFromFilter] = useState(false);

  // Смена фильтра возвращает на первую страницу — иначе можно «застрять» на
  // несуществующей странице сильно меньшего отфильтрованного набора. Значение-«всё»
  // (ALL_TENANTS/ALL_STATUSES) убирает ключ из URL.
  const changeFilterParam = (key: string, value: string, allSentinel: string) => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (value === allSentinel) next.delete(key);
        else next.set(key, value);
        return next;
      },
      { replace: true }
    );
    setPage(1);
  };

  const changeTenantFilter = (value: string) => changeFilterParam("tenantId", value, ALL_TENANTS);
  const changeStatusFilter = (value: string) =>
    changeFilterParam("publishStatus", value, ALL_STATUSES);
  // MLC-150 — toggle «не найдена в кластере»: true пишет ключ, иначе удаляет (как sentinel-«всё»).
  const toggleNotInClusterFilter = (checked: boolean) =>
    changeFilterParam("notInCluster", checked ? "true" : "false", "false");
  const resetFilters = () => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete("tenantId");
        next.delete("publishStatus");
        next.delete("notInCluster");
        next.delete("search");
        return next;
      },
      { replace: true }
    );
    setPage(1);
  };
  const anyFilterActive =
    tenantFilter !== ALL_TENANTS ||
    statusFilter !== ALL_STATUSES ||
    notInClusterFilter ||
    searchParam !== "";

  const items = useMemo<InfobaseListItem[]>(() => data?.items ?? [], [data]);
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);

  // MLC-093 — нераспределённые базы кластера: запрос только для админа (Viewer ничего
  // нового не видит, endpoint Admin-only). Питает баннер на «Базах» и discovery-first
  // флоу кнопки «Добавить».
  const unassigned = useUnassignedInfobases(isAdmin);
  const unassignedAvailable = unassigned.data?.available ?? false;
  const unassignedItems = useMemo(() => unassigned.data?.items ?? [], [unassigned.data]);
  const showUnassignedBanner = isAdmin && unassignedAvailable && unassignedItems.length > 0;

  // MLC-096 — обратный дрейф: записи панели, чьего UUID нет в кластере. BE отдаёт
  // missingItems только при available:true, но проверяем явно — ложных красных меток быть
  // не должно. membership-набор UUID пробрасывается в строки (Viewer / available:false →
  // данных нет → набор пуст → меток нет).
  const missingItems = useMemo(
    () => (isAdmin && unassignedAvailable ? (unassigned.data?.missingItems ?? []) : []),
    [isAdmin, unassignedAvailable, unassigned.data]
  );
  const missingSet = useMemo(
    () => new Set(missingItems.map((m) => m.clusterInfobaseId)),
    [missingItems]
  );
  const showMissingBanner = missingItems.length > 0;

  const openManualForm = () => {
    setEditing(null);
    setPrefill(null);
    setFormOpen(true);
  };

  // «Добавить базу» = discovery-first: при доступном RAS открываем диалог разбора,
  // при недоступном (или пока неизвестно — данные ещё грузятся, available:false) —
  // сразу ручная форма, поведение не деградирует.
  const handleOpenAdd = () => {
    if (unassignedAvailable) setUnassignedOpen(true);
    else openManualForm();
  };

  // «Назначить» из диалога — форма с префиллом выбранной базы кластера.
  const handleAssign = (item: UnassignedInfobaseItem) => {
    setUnassignedOpen(false);
    setEditing(null);
    setPrefill({ clusterInfobaseId: item.clusterInfobaseId, name: item.name });
    setFormOpen(true);
  };

  // «Ввести вручную» из диалога — текущая пустая форма.
  const handleManualEntry = () => {
    setUnassignedOpen(false);
    openManualForm();
  };

  // «Удалить» из диалога обратного дрейфа — существующий delete-флоу (подтверждение по
  // имени + аудит); после удаления инвалидация unassigned-query уберёт строку и метку.
  const handleDeleteMissing = (item: MissingInfobase) => {
    setMissingOpen(false);
    setDeleting({ id: item.infobaseId, name: item.name });
  };

  const handleOpenEdit = (infobase: InfobaseListItem) => {
    setEditing(infobase);
    setPrefill(null);
    setFormOpen(true);
  };

  // MLC-113: при удалении со страницы «Базы» прокидываем publishStatus, чтобы диалог
  // удаления показал чекбокс «снять публикацию из IIS» (диалог обратного дрейфа статус
  // не передаёт — там чекбокс скрыт).
  const handleOpenDelete = (infobase: InfobaseListItem) =>
    setDeleting({
      id: infobase.id,
      name: infobase.name,
      publishStatus: infobase.publication.lastCheckStatus,
    });

  const handleCheck = async (publication: PublicationListItem) => {
    setCheckingId(publication.id);
    try {
      await checkStatus.mutateAsync(publication.id);
      toast.success(t("publications.toasts.checkCompleted"));
    } catch {
      toast.error(t("errors.generic"));
    } finally {
      setCheckingId(null);
    }
  };

  const toggleSelect = (item: InfobaseListItem, checked: boolean) => {
    // Поэлементная правка делает выбор уже не «чисто по фильтру» — снимаем сводку фильтра.
    setSelectionFromFilter(false);
    setSelected((prev) => {
      const next = new Map(prev);
      if (checked) next.set(item.publication.id, toPublicationListItem(item));
      else next.delete(item.publication.id);
      return next;
    });
  };

  // «Выбрать все (страница)» оперирует строками текущей страницы.
  const toggleAll = (checked: boolean) => {
    setSelectionFromFilter(false);
    setSelected((prev) => {
      const next = new Map(prev);
      for (const item of items) {
        if (checked) next.set(item.publication.id, toPublicationListItem(item));
        else next.delete(item.publication.id);
      }
      return next;
    });
  };

  const clearSelection = () => {
    setSelectionFromFilter(false);
    setSelected(new Map());
  };

  // MLC-181c — «Выбрать все N по фильтру»: дёргаем /infobases/ids по ТЕКУЩЕМУ фильтру и
  // наполняем тот же внешний выбор всеми пригодными для bulk строками (через 2+ страницы).
  // capped ⇒ набор сверх кэпа: ничего не выбираем, просим уточнить фильтр. Источник полей
  // source/lastCheckStatus тут отсутствует (облегчённый контракт) — для них безопасный
  // дефолт «Unknown»: confirm=true всё равно идёт на все элементы, а bulk-диалог показывает
  // явный N и сводку фильтра.
  const handleSelectAllFiltered = async () => {
    setSelectingAllFiltered(true);
    try {
      const response = await fetchInfobaseBulkIds(
        tenantIdParam,
        publishStatusParam,
        notInClusterFilter,
        searchParam
      );
      if (response.capped) {
        toast.error(t("publications.bulk.selectAllFilteredCapped", { count: response.total }));
        return;
      }
      setSelected(() => {
        const next = new Map<string, PublicationListItem>();
        for (const row of response.items) {
          next.set(row.publicationId, {
            id: row.publicationId,
            infobaseId: row.infobaseId,
            infobaseName: row.infobaseName,
            tenantId: "",
            tenantName: "",
            siteName: row.siteName,
            virtualPath: row.virtualPath,
            platformVersion: "",
            source: "Unknown",
            lastCheckStatus: "Unknown",
            lastCheckAt: null,
            lastCheckDetails: null,
          });
        }
        return next;
      });
      setSelectionFromFilter(true);
    } catch {
      toast.error(t("errors.generic"));
    } finally {
      setSelectingAllFiltered(false);
    }
  };

  // После прогона снимаем успешные из выделения — упавшие/пропущенные остаются для повтора.
  const deselectSucceeded = (states: BulkItemState[]) =>
    setSelected((prev) => {
      const next = new Map(prev);
      for (const s of states) if (s.status === "ok") next.delete(s.id);
      return next;
    });

  const selectedPublications = useMemo(() => Array.from(selected.values()), [selected]);

  // MLC-181c — человекочитаемая сводка активного фильтра (клиент / статус / поиск) для
  // bulk-диалога, когда выбор сделан через «Выбрать все по фильтру»: оператор видит, к чему
  // применяет действие. null, когда выбор поэлементный/постраничный (сводка не показывается).
  const filterSummary = useMemo(() => {
    if (!selectionFromFilter) return null;
    const parts: string[] = [];
    if (tenantIdParam) {
      parts.push(
        t("infobases.bulk.filterSummary.tenant", {
          name: tenantNameById.get(tenantIdParam) ?? tenantIdParam,
        })
      );
    }
    if (publishStatusParam) {
      parts.push(
        t("infobases.bulk.filterSummary.status", {
          status: t(`infobases.filters.publishStatusOptions.${publishStatusParam}`),
        })
      );
    }
    if (notInClusterFilter) {
      parts.push(t("infobases.bulk.filterSummary.notInCluster"));
    }
    if (searchParam) {
      parts.push(t("infobases.bulk.filterSummary.search", { term: searchParam }));
    }
    return parts.length > 0 ? parts.join("; ") : t("infobases.bulk.filterSummary.allBases");
  }, [
    selectionFromFilter,
    tenantIdParam,
    publishStatusParam,
    notInClusterFilter,
    searchParam,
    tenantNameById,
    t,
  ]);

  const allSelected = items.length > 0 && items.every((i) => selected.has(i.publication.id));
  const someSelected = items.some((i) => selected.has(i.publication.id));
  const headerChecked: boolean | "indeterminate" = allSelected
    ? true
    : someSelected
      ? "indeterminate"
      : false;

  // Публикационные операции передаются только админу; Viewer видит read-only таблицу
  // (бэкапы доступны обеим ролям, ADR-27).
  const rowPublicationProps = isAdmin
    ? {
        onCheck: (p: PublicationListItem) => void handleCheck(p),
        onPublish: setPublishTarget,
        onUnpublish: setUnpublishTarget,
        onChangePlatform: setPlatformTarget,
      }
    : {};

  const columns = useMemo(
    () =>
      buildInfobaseColumns({
        t,
        isAdmin,
        tenantNameById,
        checkingId,
        missingSet,
        missingCheckedAtUtc: unassigned.data?.checkedAtUtc,
        isSelected: (id) => selected.has(id),
        onToggleSelect: toggleSelect,
        headerChecked,
        onToggleAll: toggleAll,
        selectionDisabled: items.length === 0,
        onEdit: handleOpenEdit,
        onDelete: handleOpenDelete,
        onReassign: tenants.length > 1 ? setReassigning : undefined,
        onBackups: setBackupsFor,
        ...rowPublicationProps,
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      t,
      isAdmin,
      tenantNameById,
      checkingId,
      missingSet,
      unassigned.data?.checkedAtUtc,
      selected,
      headerChecked,
      items,
      tenants.length,
    ]
  );

  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
    // Серверная пагинация и фильтрация: tanstack не режет/не фильтрует данные сам.
    manualPagination: true,
    manualFiltering: true,
    pageCount: totalPages,
    // Стабильный id строки по инфобазе — выделение и data-state не «прыгают» при пагинации.
    getRowId: (row) => row.id,
  });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("infobases.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("infobases.subtitle")}</p>
        </div>
        {isAdmin && (
          <Button onClick={handleOpenAdd} disabled={tenants.length === 0}>
            <PlusIcon className="size-4" />
            {t("infobases.actions.add")}
          </Button>
        )}
      </div>

      <Tabs defaultValue="bases">
        <TabsList>
          <TabsTrigger value="bases">{t("infobases.tabs.bases")}</TabsTrigger>
          <TabsTrigger value="iis">{t("infobases.tabs.iis")}</TabsTrigger>
        </TabsList>

        <TabsContent value="bases" className="space-y-6">
          {isError && (
            <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
              <p className="font-medium">{t("infobases.errors.loadFailed")}</p>
              <Button
                variant="link"
                className="px-0"
                onClick={() => {
                  void refetch().then((r) => {
                    if (r.isSuccess) toast.success(t("common.refresh"));
                  });
                }}
              >
                {t("common.refresh")}
              </Button>
            </div>
          )}

          {showUnassignedBanner && unassigned.data && (
            <UnassignedBanner
              count={unassignedItems.length}
              checkedAtUtc={unassigned.data.checkedAtUtc}
              onRefresh={() => void unassigned.refresh()}
              onResolve={() => setUnassignedOpen(true)}
              isRefreshing={unassigned.isFetching}
            />
          )}

          {showMissingBanner && unassigned.data && (
            <MissingInfobasesBanner
              count={missingItems.length}
              checkedAtUtc={unassigned.data.checkedAtUtc}
              onShow={() => setMissingOpen(true)}
            />
          )}

          {/* MLC-150 — фильтр «не найдена в кластере» запрошен, но RAS недоступен:
              честное состояние вместо вводящего в заблуждение «0 найдено». */}
          {clusterUnavailable && (
            <div className="flex flex-wrap items-center gap-3 rounded-md border border-amber-600/40 bg-amber-600/5 p-3 text-sm text-amber-700 dark:text-amber-300">
              <AlertTriangleIcon className="size-5 shrink-0" />
              <span className="font-medium">{t("infobases.filters.notInClusterUnavailable")}</span>
              <Button
                variant="outline"
                size="sm"
                className="ml-auto"
                onClick={() => void refetch()}
              >
                {t("common.refresh")}
              </Button>
            </div>
          )}

          {isAdmin && selected.size > 0 && (
            <PublicationsBulkBar
              count={selected.size}
              onPublish={() => setBulkPublishOpen(true)}
              onChangePlatform={() => setBulkPlatformOpen(true)}
              onCheck={() => setBulkCheckOpen(true)}
              onUnpublish={() => setBulkUnpublishOpen(true)}
              onDeleteInfobase={() => setBulkDeleteOpen(true)}
              onClear={clearSelection}
              onSelectAllFiltered={() => void handleSelectAllFiltered()}
              isSelectingAllFiltered={selectingAllFiltered}
            />
          )}

          <TooltipProvider delayDuration={150}>
            <DataTable
              table={table}
              density={density}
              onToggleDensity={toggleDensity}
              isLoading={isLoading}
              skeletonRows={4}
              columnLabel={(id) => table.getColumn(id)?.columnDef.meta?.label ?? id}
              renderSkeletonRow={() => <InfobaseSkeletonCells columnCount={columns.length} />}
              emptyState={
                isError || clusterUnavailable ? undefined : ( // RAS недоступен — баннер выше объясняет пустоту, ложный «нет инфобаз» не показываем
                  <div className="flex flex-col items-center justify-center gap-3 text-center">
                    <DatabaseIcon className="text-muted-foreground size-8" />
                    <div className="space-y-1">
                      <p className="font-medium">
                        {notInClusterFilter
                          ? t("infobases.filters.notInClusterEmpty")
                          : t("infobases.empty.title")}
                      </p>
                      <p className="text-muted-foreground text-sm">
                        {tenants.length === 0
                          ? t("infobases.empty.noTenantsHint")
                          : t("infobases.empty.hint")}
                      </p>
                    </div>
                    {isAdmin && tenants.length > 0 && !notInClusterFilter && (
                      <Button size="sm" onClick={handleOpenAdd}>
                        <PlusIcon className="size-4" />
                        {t("infobases.actions.add")}
                      </Button>
                    )}
                  </div>
                )
              }
              toolbarChildren={
                <>
                  <Input
                    type="search"
                    className="max-w-xs"
                    placeholder={t("infobases.searchPlaceholder")}
                    value={searchDraft}
                    onChange={(e) => setSearchDraft(e.target.value)}
                    aria-label={t("infobases.searchPlaceholder")}
                  />
                  <SearchableSelect
                    options={tenants.map((tenant) => ({
                      value: tenant.id,
                      label: tenant.name,
                    }))}
                    value={tenantIdParam}
                    onChange={(v) => changeTenantFilter(v ?? ALL_TENANTS)}
                    placeholder={t("infobases.filters.allTenants")}
                    searchPlaceholder={t("common.search")}
                    aria-label={t("infobases.filters.tenant")}
                    triggerClassName="w-72"
                  />
                  <Select value={statusFilter} onValueChange={changeStatusFilter}>
                    <SelectTrigger className="w-56">
                      <SelectValue placeholder={t("infobases.filters.publishStatus")} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ALL_STATUSES}>
                        {t("infobases.filters.allStatuses")}
                      </SelectItem>
                      {PUBLISH_STATUS_FILTERS.map((status) => (
                        <SelectItem key={status} value={status}>
                          {t(`infobases.filters.publishStatusOptions.${status}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {isAdmin && (
                    <label className="flex items-center gap-2 text-sm whitespace-nowrap select-none">
                      <Checkbox
                        checked={notInClusterFilter}
                        onCheckedChange={(checked) => toggleNotInClusterFilter(checked === true)}
                      />
                      {t("infobases.filters.notInCluster")}
                    </label>
                  )}
                  {anyFilterActive && (
                    <Button variant="ghost" size="sm" onClick={resetFilters}>
                      {t("common.reset")}
                    </Button>
                  )}
                </>
              }
            />
          </TooltipProvider>

          <PaginationBar
            page={currentPage}
            pageSize={PAGE_SIZE}
            total={total}
            onPageChange={setPage}
            isFetching={isFetching && !isLoading}
          />
        </TabsContent>

        <TabsContent value="iis">
          <IisManagementCard isAdmin={isAdmin} />
        </TabsContent>
      </Tabs>

      <InfobaseFormDialog
        key={editing?.id ?? (prefill ? `create-${prefill.clusterInfobaseId}` : "create")}
        open={formOpen}
        onOpenChange={setFormOpen}
        infobase={editing}
        tenants={tenants}
        defaultTenantId={tenantFilter !== ALL_TENANTS ? tenantFilter : undefined}
        prefill={prefill}
      />

      {isAdmin && (
        <UnassignedInfobasesDialog
          open={unassignedOpen}
          onOpenChange={setUnassignedOpen}
          items={unassignedItems}
          hiddenItems={unassigned.data?.hiddenItems ?? []}
          available={unassignedAvailable}
          checkedAtUtc={unassigned.data?.checkedAtUtc ?? null}
          isLoading={unassigned.isLoading}
          isRefreshing={unassigned.isFetching}
          onRefresh={() => void unassigned.refresh()}
          onAssign={handleAssign}
          onManualEntry={handleManualEntry}
        />
      )}
      {isAdmin && (
        <MissingInfobasesDialog
          open={missingOpen}
          onOpenChange={setMissingOpen}
          items={missingItems}
          onDelete={handleDeleteMissing}
        />
      )}
      <DeleteInfobaseDialog
        key={deleting?.id ?? "none"}
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        infobase={deleting}
      />
      <ReassignInfobaseDialog
        key={reassigning?.id ?? "no-reassign"}
        open={reassigning !== null}
        onOpenChange={(open) => {
          if (!open) setReassigning(null);
        }}
        infobase={reassigning}
        tenants={tenants}
      />
      <BackupsDialog
        key={backupsFor?.id ?? "no-backups"}
        open={backupsFor !== null}
        onOpenChange={(open) => {
          if (!open) setBackupsFor(null);
        }}
        infobase={backupsFor}
      />

      {isAdmin && (
        <>
          <PublishPublicationDialog
            key={`publish-${publishTarget?.id ?? "new"}`}
            open={publishTarget !== null}
            onOpenChange={(open) => {
              if (!open) setPublishTarget(null);
            }}
            publication={publishTarget}
          />
          <UnpublishPublicationDialog
            key={`unpublish-${unpublishTarget?.id ?? "new"}`}
            open={unpublishTarget !== null}
            onOpenChange={(open) => {
              if (!open) setUnpublishTarget(null);
            }}
            publication={unpublishTarget}
          />
          <ChangePlatformDialog
            key={`platform-${platformTarget?.id ?? "new"}`}
            open={platformTarget !== null}
            onOpenChange={(open) => {
              if (!open) setPlatformTarget(null);
            }}
            publication={platformTarget}
          />
          <BulkPublishDialog
            open={bulkPublishOpen}
            onOpenChange={setBulkPublishOpen}
            publications={selectedPublications}
            onRunComplete={deselectSucceeded}
            filterSummary={filterSummary}
          />
          <BulkChangePlatformDialog
            open={bulkPlatformOpen}
            onOpenChange={setBulkPlatformOpen}
            publications={selectedPublications}
            onRunComplete={deselectSucceeded}
            filterSummary={filterSummary}
          />
          <BulkCheckDialog
            open={bulkCheckOpen}
            onOpenChange={setBulkCheckOpen}
            publications={selectedPublications}
            onRunComplete={deselectSucceeded}
            filterSummary={filterSummary}
          />
          <BulkUnpublishDialog
            open={bulkUnpublishOpen}
            onOpenChange={setBulkUnpublishOpen}
            publications={selectedPublications}
            onRunComplete={deselectSucceeded}
            filterSummary={filterSummary}
          />
          <BulkDeleteInfobaseDialog
            open={bulkDeleteOpen}
            onOpenChange={setBulkDeleteOpen}
            publications={selectedPublications}
            onRunComplete={deselectSucceeded}
            filterSummary={filterSummary}
          />
        </>
      )}
    </div>
  );
}

// Скелетон строки повторяет число колонок страницы (включая чекбокс для админа).
function InfobaseSkeletonCells({ columnCount }: { columnCount: number }) {
  return (
    <>
      {Array.from({ length: columnCount }).map((_, idx) => (
        <TableCell key={idx}>
          <Skeleton className="h-4 w-24" />
        </TableCell>
      ))}
    </>
  );
}
