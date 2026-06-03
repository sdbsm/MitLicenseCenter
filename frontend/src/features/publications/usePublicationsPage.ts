import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { useQueryClient } from "@tanstack/react-query";
import { useMe } from "@/features/auth/useAuth";
import { useTenants } from "@/features/tenants/useTenants";
import type { PublicationListItem } from "./types";
import { parseParams, type UrlFilters } from "./urlState";
import {
  driftStatusQueryKey,
  fetchDriftStatus,
  publicationsQueryKey,
  useCheckDrift,
  usePublications,
} from "./usePublications";

const POLL_TIMEOUT_MS = 30_000;
const POLL_INTERVAL_MS = 2_000;

/**
 * Оркестрация страницы публикаций: загрузка списка/клиентов, URL-фильтры (клиент + drift-статус),
 * polling проверки дрейфа (single-flight через ref на interval) и состояние диалога reconcile.
 * Презентация (таблица, фильтры) вынесена в отдельные компоненты — поведение 1:1 с прежней
 * монолитной страницей (MLC-032).
 */
export function usePublicationsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const { tenantId, driftStatus } = useMemo(() => parseParams(searchParams), [searchParams]);

  const { data: publications, isLoading, isError, refetch } = usePublications();
  const { data: tenantsData } = useTenants();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  const [pollingId, setPollingId] = useState<string | null>(null);
  const pollIntervalRef = useRef<number | null>(null);
  const [selectedPublication, setSelectedPublication] = useState<PublicationListItem | null>(null);
  const [reconcileOpen, setReconcileOpen] = useState(false);

  const checkDrift = useCheckDrift();

  useEffect(() => {
    return () => {
      if (pollIntervalRef.current !== null) {
        clearInterval(pollIntervalRef.current);
        pollIntervalRef.current = null;
      }
    };
  }, []);

  const filtered = useMemo(() => {
    const rows = publications ?? [];
    return rows.filter((p) => {
      if (tenantId && p.tenantId !== tenantId) return false;
      if (driftStatus && p.lastDriftStatus !== driftStatus) return false;
      return true;
    });
  }, [publications, tenantId, driftStatus]);

  const setFilter = (next: Partial<UrlFilters>) => {
    const params = new URLSearchParams();
    const newTenantId = next.tenantId !== undefined ? next.tenantId : tenantId;
    const newDriftStatus = next.driftStatus !== undefined ? next.driftStatus : driftStatus;
    if (newTenantId) params.set("tenantId", newTenantId);
    if (newDriftStatus) params.set("driftStatus", newDriftStatus);
    setSearchParams(params, { replace: true });
  };

  const stopPolling = () => {
    if (pollIntervalRef.current !== null) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    setPollingId(null);
  };

  const handleCheckDrift = async (publication: PublicationListItem) => {
    if (pollIntervalRef.current !== null) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    try {
      await checkDrift.mutateAsync(publication.id);
    } catch {
      toast.error(t("errors.generic"));
      return;
    }
    toast.info(t("publications.toasts.checkStarted"));
    setPollingId(publication.id);

    const startedAt = Date.now();
    const initialCheckedAt = publication.lastDriftCheckAt;

    pollIntervalRef.current = window.setInterval(async () => {
      try {
        const status = await fetchDriftStatus(publication.id);
        queryClient.setQueryData(driftStatusQueryKey(publication.id), status);
        if (status.checkedAt && status.checkedAt !== initialCheckedAt) {
          stopPolling();
          void queryClient.invalidateQueries({ queryKey: publicationsQueryKey });
          toast.success(t("publications.toasts.checkCompleted"));
          return;
        }
        if (Date.now() - startedAt > POLL_TIMEOUT_MS) {
          stopPolling();
          toast.warning(t("publications.toasts.checkTimeout"));
        }
      } catch {
        stopPolling();
        toast.error(t("errors.generic"));
      }
    }, POLL_INTERVAL_MS);
  };

  const handleReconcileClick = (publication: PublicationListItem) => {
    setSelectedPublication(publication);
    setReconcileOpen(true);
  };

  const handleReconcileOpenChange = (open: boolean) => {
    setReconcileOpen(open);
    if (!open) setSelectedPublication(null);
  };

  return {
    isLoading,
    isError,
    refetch,
    isAdmin,
    tenants: tenantsData?.items ?? [],
    tenantId,
    driftStatus,
    setFilter,
    filtered,
    hasAnyPublications: (publications ?? []).length > 0,
    pollingId,
    handleCheckDrift,
    selectedPublication,
    reconcileOpen,
    handleReconcileClick,
    handleReconcileOpenChange,
  };
}
