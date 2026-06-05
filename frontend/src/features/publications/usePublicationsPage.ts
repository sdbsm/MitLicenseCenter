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

  const checkStatus = useCheckStatus();

  const filtered = useMemo(() => {
    const rows = publications ?? [];
    return rows.filter((p) => {
      if (tenantId && p.tenantId !== tenantId) return false;
      if (status && p.lastCheckStatus !== status) return false;
      return true;
    });
  }, [publications, tenantId, status]);

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
  };
}
