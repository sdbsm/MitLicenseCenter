import { PlayIcon, PowerIcon, SquareIcon } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { ApiError, readConflictBody } from "@/lib/api";
import { IisAppPoolsList } from "./IisAppPoolsList";
import { IisConfirmDialog } from "./IisConfirmDialog";
import { IisSitesList } from "./IisSitesList";
import { IisStateBadge } from "./IisStateBadge";
import {
  useIisAppPools,
  useIisServerStatus,
  useIisSites,
  useRecyclePool,
  useResetIis,
  useRestartSite,
  useStartIis,
  useStartPool,
  useStartSite,
  useStopIis,
  useStopPool,
  useStopSite,
} from "./useIisManagement";

// Разрушительные операции проходят через confirm-диалог; start выполняется сразу.
type ConfirmAction =
  | { kind: "recyclePool"; name: string }
  | { kind: "stopPool"; name: string }
  | { kind: "stopSite"; name: string }
  | { kind: "restartSite"; name: string }
  | { kind: "reset" }
  | { kind: "stopIis" };

interface IisManagementCardProps {
  isAdmin: boolean;
}

// MLC-047 (ADR-24): массовый блок управления IIS на странице публикаций. Глобальный
// iisreset + списки пулов и сайтов с действиями. Viewer видит состояние, Admin
// выполняет операции. Разрушительные (recycle/stop/restart/iisreset) — через confirm-диалог.
export function IisManagementCard({ isAdmin }: IisManagementCardProps) {
  const { t } = useTranslation();

  const server = useIisServerStatus();
  const pools = useIisAppPools();
  const sites = useIisSites();
  const serverState = server.data?.available ? server.data.state : "Unknown";
  const serverRunning = serverState === "Started" || serverState === "Starting";

  const recyclePool = useRecyclePool();
  const startPool = useStartPool();
  const stopPool = useStopPool();
  const startSite = useStartSite();
  const stopSite = useStopSite();
  const restartSite = useRestartSite();
  const resetIis = useResetIis();
  const stopIis = useStopIis();
  const startIis = useStartIis();

  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);

  const busy =
    recyclePool.isPending ||
    startPool.isPending ||
    stopPool.isPending ||
    startSite.isPending ||
    stopSite.isPending ||
    restartSite.isPending ||
    resetIis.isPending ||
    stopIis.isPending ||
    startIis.isPending;

  const reportError = (error: unknown) => {
    if (error instanceof ApiError && error.status === 409) {
      toast.error(readConflictBody(error)?.detail ?? t("publications.iis.toasts.failed"));
      return;
    }
    if (error instanceof ApiError && error.status === 404) {
      toast.error(t("publications.iis.toasts.notFound"));
      return;
    }
    toast.error(t("errors.generic"));
  };

  // Start пула/сайта — не разрушительно, без токена.
  const runStartPool = async (name: string) => {
    try {
      await startPool.mutateAsync(name);
      toast.success(t("publications.iis.toasts.poolStarted"));
    } catch (error) {
      reportError(error);
    }
  };
  const runStartSite = async (name: string) => {
    try {
      await startSite.mutateAsync(name);
      toast.success(t("publications.iis.toasts.siteStarted"));
    } catch (error) {
      reportError(error);
    }
  };
  // Запуск всего IIS (iisreset /start) — восстановление, без токена.
  const runStartIis = async () => {
    try {
      await startIis.mutateAsync();
      toast.success(t("publications.iis.toasts.started"));
    } catch (error) {
      reportError(error);
    }
  };

  const pendingFor = (action: ConfirmAction): boolean => {
    switch (action.kind) {
      case "recyclePool":
        return recyclePool.isPending;
      case "stopPool":
        return stopPool.isPending;
      case "stopSite":
        return stopSite.isPending;
      case "restartSite":
        return restartSite.isPending;
      case "reset":
        return resetIis.isPending;
      case "stopIis":
        return stopIis.isPending;
    }
  };

  const runConfirmed = async (action: ConfirmAction) => {
    try {
      switch (action.kind) {
        case "recyclePool":
          await recyclePool.mutateAsync(action.name);
          toast.success(t("publications.iis.toasts.recycled"));
          break;
        case "stopPool":
          await stopPool.mutateAsync(action.name);
          toast.success(t("publications.iis.toasts.poolStopped"));
          break;
        case "stopSite":
          await stopSite.mutateAsync(action.name);
          toast.success(t("publications.iis.toasts.siteStopped"));
          break;
        case "restartSite":
          await restartSite.mutateAsync(action.name);
          toast.success(t("publications.iis.toasts.siteRestarted"));
          break;
        case "reset":
          await resetIis.mutateAsync();
          toast.success(t("publications.iis.toasts.reset"));
          break;
        case "stopIis":
          await stopIis.mutateAsync();
          toast.success(t("publications.iis.toasts.stopped"));
          break;
      }
      setConfirmAction(null);
    } catch (error) {
      reportError(error);
    }
  };

  const confirmConfig = (action: ConfirmAction) => {
    switch (action.kind) {
      case "recyclePool":
        return {
          title: t("publications.iis.recycle.title"),
          description: t("publications.iis.recycle.description", { name: action.name }),
          confirmLabel: t("publications.iis.actions.recycle"),
        };
      case "stopPool":
        return {
          title: t("publications.iis.stopPool.title"),
          description: t("publications.iis.stopPool.description", { name: action.name }),
          confirmLabel: t("publications.iis.actions.stop"),
        };
      case "stopSite":
        return {
          title: t("publications.iis.stopSite.title"),
          description: t("publications.iis.stopSite.description", { name: action.name }),
          confirmLabel: t("publications.iis.actions.stop"),
        };
      case "restartSite":
        return {
          title: t("publications.iis.restartSite.title"),
          description: t("publications.iis.restartSite.description", { name: action.name }),
          confirmLabel: t("publications.iis.actions.restart"),
        };
      case "reset":
        return {
          title: t("publications.iis.reset.title"),
          description: t("publications.iis.reset.description"),
          confirmLabel: t("publications.iis.reset.confirm"),
        };
      case "stopIis":
        return {
          title: t("publications.iis.stopIis.title"),
          description: t("publications.iis.stopIis.description"),
          confirmLabel: t("publications.iis.stopIis.confirm"),
        };
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          {t("publications.iis.title")}
          <IisStateBadge state={serverState} />
        </CardTitle>
        <CardDescription>{t("publications.iis.description")}</CardDescription>
        {isAdmin && (
          <CardAction className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={busy}
              onClick={() => setConfirmAction({ kind: "reset" })}
            >
              <PowerIcon className="size-4" />
              {t("publications.iis.reset.button")}
            </Button>
            {serverRunning ? (
              <Button
                variant="destructive"
                size="sm"
                disabled={busy}
                onClick={() => setConfirmAction({ kind: "stopIis" })}
              >
                <SquareIcon className="size-4" />
                {t("publications.iis.stopIis.button")}
              </Button>
            ) : (
              <Button
                variant="outline"
                size="sm"
                disabled={busy}
                className="border-transparent bg-emerald-600 text-white hover:bg-emerald-700"
                onClick={() => void runStartIis()}
              >
                <PlayIcon className="size-4" />
                {t("publications.iis.start.button")}
              </Button>
            )}
          </CardAction>
        )}
      </CardHeader>

      <CardContent className="space-y-6">
        <section className="space-y-2">
          <h3 className="text-sm font-medium">{t("publications.iis.pools.title")}</h3>
          {pools.isLoading ? (
            <p className="text-muted-foreground text-sm">{t("common.loading")}</p>
          ) : pools.data && !pools.data.available ? (
            <p className="text-destructive text-sm">
              {pools.data.error ?? t("publications.iis.unavailable")}
            </p>
          ) : pools.isError ? (
            <p className="text-destructive text-sm">{t("publications.iis.unavailable")}</p>
          ) : (
            <IisAppPoolsList
              pools={pools.data?.items ?? []}
              isAdmin={isAdmin}
              busy={busy}
              onRecycle={(name) => setConfirmAction({ kind: "recyclePool", name })}
              onStart={(name) => void runStartPool(name)}
              onStop={(name) => setConfirmAction({ kind: "stopPool", name })}
            />
          )}
        </section>

        <section className="space-y-2">
          <h3 className="text-sm font-medium">{t("publications.iis.sites.title")}</h3>
          {sites.isLoading ? (
            <p className="text-muted-foreground text-sm">{t("common.loading")}</p>
          ) : sites.data && !sites.data.available ? (
            <p className="text-destructive text-sm">
              {sites.data.error ?? t("publications.iis.unavailable")}
            </p>
          ) : sites.isError ? (
            <p className="text-destructive text-sm">{t("publications.iis.unavailable")}</p>
          ) : (
            <IisSitesList
              sites={sites.data?.items ?? []}
              isAdmin={isAdmin}
              busy={busy}
              onStart={(name) => void runStartSite(name)}
              onStop={(name) => setConfirmAction({ kind: "stopSite", name })}
              onRestart={(name) => setConfirmAction({ kind: "restartSite", name })}
            />
          )}
        </section>
      </CardContent>

      {confirmAction && (
        <IisConfirmDialog
          key={
            "name" in confirmAction
              ? `${confirmAction.kind}:${confirmAction.name}`
              : confirmAction.kind
          }
          open
          onOpenChange={(open) => {
            if (!open) setConfirmAction(null);
          }}
          pending={pendingFor(confirmAction)}
          onConfirm={() => void runConfirmed(confirmAction)}
          {...confirmConfig(confirmAction)}
        />
      )}
    </Card>
  );
}
