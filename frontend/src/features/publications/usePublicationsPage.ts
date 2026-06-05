import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { useMe } from "@/features/auth/useAuth";
import { useTenants } from "@/features/tenants/useTenants";
import type { PublicationListItem } from "./types";
import { parseParams, type UrlFilters } from "./urlState";
import { useCheckStatus, usePublications } from "./usePublications";

/**
 * Оркестрация страницы публикаций (MLC-045): загрузка списка/клиентов, URL-фильтры
 * (клиент + статус публикации), read-only проверка «Проверить сейчас» и состояние
 * диалогов «Опубликовать (webinst)» и «Сменить платформу». Презентация — в отдельных
 * компонентах (контейнер/хук/части, MLC-032).
 */
export function usePublicationsPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { tenantId, status } = useMemo(() => parseParams(searchParams), [searchParams]);

  const { data: publications, isLoading, isError, refetch } = usePublications();
  const { data: tenantsData } = useTenants();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  const [checkingId, setCheckingId] = useState<string | null>(null);
  const [publishTarget, setPublishTarget] = useState<PublicationListItem | null>(null);
  const [platformTarget, setPlatformTarget] = useState<PublicationListItem | null>(null);

  // MLC-046: множественный выбор для массовых операций. Храним id; выбор переживает
  // смену фильтра (бар показывает общий счётчик). Объекты для операции берём из полного
  // списка, чтобы спрятанные фильтром строки тоже корректно обрабатывались.
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkPublishOpen, setBulkPublishOpen] = useState(false);
  const [bulkPlatformOpen, setBulkPlatformOpen] = useState(false);

  const checkStatus = useCheckStatus();

  const filtered = useMemo(() => {
    const rows = publications ?? [];
    return rows.filter((p) => {
      if (tenantId && p.tenantId !== tenantId) return false;
      if (status && p.lastCheckStatus !== status) return false;
      return true;
    });
  }, [publications, tenantId, status]);

  const selectedPublications = useMemo(
    () => (publications ?? []).filter((p) => selectedIds.has(p.id)),
    [publications, selectedIds]
  );

  const toggleSelect = (id: string, checked: boolean) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });

  // «Выбрать все» оперирует текущими отфильтрованными строками.
  const toggleAll = (checked: boolean) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const p of filtered) {
        if (checked) next.add(p.id);
        else next.delete(p.id);
      }
      return next;
    });

  const clearSelection = () => setSelectedIds(new Set());

  // После прогона снимаем успешные из выделения — упавшие/пропущенные остаются для повтора.
  const deselectSucceeded = (states: { id: string; status: string }[]) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const s of states) if (s.status === "ok") next.delete(s.id);
      return next;
    });

  const setFilter = (next: Partial<UrlFilters>) => {
    const params = new URLSearchParams();
    const newTenantId = next.tenantId !== undefined ? next.tenantId : tenantId;
    const newStatus = next.status !== undefined ? next.status : status;
    if (newTenantId) params.set("tenantId", newTenantId);
    if (newStatus) params.set("status", newStatus);
    setSearchParams(params, { replace: true });
  };

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

  return {
    isLoading,
    isError,
    refetch,
    isAdmin,
    tenants: tenantsData?.items ?? [],
    tenantId,
    status,
    setFilter,
    filtered,
    hasAnyPublications: (publications ?? []).length > 0,
    checkingId,
    handleCheck,
    publishTarget,
    openPublish: setPublishTarget,
    platformTarget,
    openPlatform: setPlatformTarget,
    // MLC-046: выбор + массовые операции.
    selectedIds,
    toggleSelect,
    toggleAll,
    clearSelection,
    deselectSucceeded,
    selectedPublications,
    bulkPublishOpen,
    setBulkPublishOpen,
    bulkPlatformOpen,
    setBulkPlatformOpen,
  };
}
