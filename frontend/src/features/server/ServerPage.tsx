import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError, readConflictBody } from "@/lib/api";
import { useMe } from "@/features/auth/useAuth";
import { IisManagementCard } from "./iis/IisManagementCard";
import { MaintenanceTab } from "./MaintenanceTab";
import { OneCServerActionDialog } from "./OneCServerActionDialog";
import { ServerHealthBadge } from "./ServerHealthBadge";
import {
  useOneCServerOperation,
  useServerStatus,
  type IisStatus,
  type OneCServer,
  type RasStatus,
  type ServerStatus,
  type SqlStatus,
} from "./useServerStatus";

/**
 * Раздел «Сервер» (MLC-214/215/216, ADR-54/55): сводный статус служб стека узла,
 * детальное управление IIS и обслуживание. Три вкладки: «Службы» (по умолчанию), «IIS»
 * (дом IIS — «Сервер», ADR-54: пулы/сайты/iisreset переехали сюда из «Баз») и
 * «Обслуживание» (свежесть бэкапов SQL, только чтение, MLC-216). Контент «IIS» и
 * «Обслуживание» монтируется только при активации вкладки — их запросы стреляют лениво.
 * Viewer наблюдает; Admin управляет сервером 1С и IIS (RAS/SQL — только наблюдение).
 */
export function ServerPage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("server.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("server.subtitle")}</p>
      </div>

      <Tabs defaultValue="services">
        <TabsList>
          <TabsTrigger value="services">{t("server.tabs.services")}</TabsTrigger>
          <TabsTrigger value="iis">{t("server.tabs.iis")}</TabsTrigger>
          <TabsTrigger value="maintenance">{t("server.tabs.maintenance")}</TabsTrigger>
        </TabsList>

        <TabsContent value="services">
          <ServicesTab />
        </TabsContent>

        {/* IIS-карточка монтируется при активации вкладки (Radix TabsContent) →
            запросы /iis/* не стреляют, пока оператор не открыл «IIS». */}
        <TabsContent value="iis">
          <IisManagementCard isAdmin={isAdmin} />
        </TabsContent>

        {/* Вкладка «Обслуживание» (MLC-216): свежесть бэкапов SQL, только чтение.
            Монтируется лениво — запрос /server/maintenance/backups стреляет при открытии. */}
        <TabsContent value="maintenance">
          <MaintenanceTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// Вкладка «Службы»: загрузка сводного статуса узла (светофор + 1С + сводки RAS/SQL/IIS).
// Загрузку/ошибку статуса держим здесь — вкладка «IIS» статус не требует.
function ServicesTab() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useServerStatus();

  return (
    <div className="space-y-6">
      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("server.loadFailed")}</p>
        </div>
      )}

      {isLoading && !data && (
        <div className="space-y-6">
          <Skeleton className="h-10 w-48" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      )}

      {data && <ServicesContent status={data} />}
    </div>
  );
}

function ServicesContent({ status }: { status: ServerStatus }) {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      {/* Общий индикатор здоровья узла (светофор по overall). */}
      <div className="flex items-center gap-3">
        <span className="text-sm font-medium">{t("server.health.label")}</span>
        <ServerHealthBadge overall={status.overall} />
      </div>

      <OneCServersSection servers={status.oneCServers} />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <RasSummaryCard ras={status.ras} />
        <SqlSummaryCard sql={status.sql} />
        <IisSummaryCard iis={status.iis} />
      </div>
    </div>
  );
}

// ── Серверы 1С (управление для Admin) ──────────────────────────────────────────

function OneCServersSection({ servers }: { servers: OneCServer[] }) {
  const { t } = useTranslation();

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("server.onec.title")}</CardTitle>
      </CardHeader>
      <CardContent className="grid gap-3">
        {servers.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t("server.onec.empty")}</p>
        ) : (
          servers.map((server) => <OneCServerRow key={server.serviceName} server={server} />)
        )}
      </CardContent>
    </Card>
  );
}

function OneCServerRow({ server }: { server: OneCServer }) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const startMutation = useOneCServerOperation();
  // Диалог подтверждения для разрушительных операций (stop/restart).
  const [dialogOp, setDialogOp] = useState<"stop" | "restart" | null>(null);

  const handleStart = async () => {
    try {
      await startMutation.mutateAsync({ operation: "start", serviceName: server.serviceName });
      toast.success(t("server.toasts.start"));
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        toast.error(readConflictBody(error)?.detail ?? t("server.toasts.failed"));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3">
      <div className="grid gap-0.5">
        <span className="font-medium">{server.serviceName}</span>
        {server.platformVersion && (
          <span className="text-muted-foreground text-xs">
            {t("server.onec.platform", { version: server.platformVersion })}
          </span>
        )}
      </div>

      <div className="flex items-center gap-3">
        <StatusBadge variant={server.running ? "success" : "neutral"}>
          {server.running ? t("server.onec.running") : t("server.onec.stopped")}
        </StatusBadge>

        {isAdmin &&
          (server.running ? (
            <>
              <Button
                size="sm"
                variant="outline"
                onClick={() => setDialogOp("restart")}
                disabled={startMutation.isPending}
              >
                {t("server.onec.actions.restart")}
              </Button>
              <Button
                size="sm"
                variant="outline"
                onClick={() => setDialogOp("stop")}
                disabled={startMutation.isPending}
              >
                {t("server.onec.actions.stop")}
              </Button>
            </>
          ) : (
            <Button size="sm" onClick={() => void handleStart()} disabled={startMutation.isPending}>
              {startMutation.isPending ? t("common.loading") : t("server.onec.actions.start")}
            </Button>
          ))}
      </div>

      {dialogOp && (
        <OneCServerActionDialog
          open={dialogOp !== null}
          onOpenChange={(open) => {
            if (!open) setDialogOp(null);
          }}
          operation={dialogOp}
          serviceName={server.serviceName}
        />
      )}
    </div>
  );
}

// ── Сводки наблюдения RAS / SQL / IIS (без управления) ──────────────────────────

// Карточка-обёртка сводки: заголовок + бейдж/ошибка + подпись-намёк (где управлять).
function SummaryCard({
  title,
  available,
  error,
  variant,
  label,
  detail,
  hint,
}: {
  title: string;
  available: boolean;
  error: string | null;
  variant: StatusBadgeVariant;
  label: string;
  detail?: string;
  hint?: string;
}) {
  const { t } = useTranslation();

  return (
    <Card className="gap-2 py-4">
      <CardHeader className="px-4 pb-0">
        <CardTitle className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="grid gap-1.5 px-4">
        {available ? (
          <>
            <StatusBadge variant={variant}>{label}</StatusBadge>
            {detail && <p className="text-muted-foreground text-xs">{detail}</p>}
          </>
        ) : (
          // Деградация: источник недоступен — честная плашка, не падение экрана.
          <p className="text-status-danger text-xs">{error ?? t("server.summary.unavailable")}</p>
        )}
        {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
      </CardContent>
    </Card>
  );
}

function RasSummaryCard({ ras }: { ras: RasStatus }) {
  const { t } = useTranslation();
  return (
    <SummaryCard
      title={t("server.summary.ras")}
      available={ras.available}
      error={ras.error}
      variant={ras.running ? "success" : "warning"}
      label={ras.running ? t("server.summary.running") : t("server.summary.stopped")}
      detail={t("server.summary.state", { state: ras.state })}
      hint={t("server.summary.rasHint")}
    />
  );
}

function SqlSummaryCard({ sql }: { sql: SqlStatus }) {
  const { t } = useTranslation();
  return (
    <SummaryCard
      title={t("server.summary.sql")}
      available={sql.available}
      error={sql.error}
      variant={sql.running ? "success" : "warning"}
      label={sql.running ? t("server.summary.running") : t("server.summary.stopped")}
      detail={sql.instance ? t("server.summary.instance", { instance: sql.instance }) : undefined}
    />
  );
}

function IisSummaryCard({ iis }: { iis: IisStatus }) {
  const { t } = useTranslation();
  // State у IIS строкой; «Started»/«Running» → success, иначе warning. Незнакомое → warning.
  const running = iis.state === "Started" || iis.state === "Running";
  return (
    <SummaryCard
      title={t("server.summary.iis")}
      available={iis.available}
      error={iis.error}
      variant={running ? "success" : "warning"}
      label={t(`server.summary.iisState.${iis.state}`, { defaultValue: iis.state })}
      hint={t("server.summary.iisHint")}
    />
  );
}
