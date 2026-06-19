import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  useMaintenanceBackups,
  type BackupFreshness,
  type DatabaseBackupFreshness,
} from "./useMaintenanceBackups";
import {
  alertingOutcomes,
  useMaintenancePlans,
  type MaintenancePlan,
  type MaintenancePlans,
  type MaintenanceSubplan,
} from "./useMaintenancePlans";

/**
 * Вкладка «Обслуживание» раздела «Сервер» (MLC-216, ADR-54) — ТОЛЬКО чтение: свежесть
 * резервных копий баз из msdb.dbo.backupset (live-read). Таблица: база, последний FULL/DIFF/LOG
 * и метка «устарел/свежо» (StatusBadge). Деградация (нет прав / SQL недоступен) — статусом,
 * экран не падает. Контент монтируется лениво (Radix TabsContent), запрос стреляет при открытии.
 */
export function MaintenanceTab() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useMaintenanceBackups();

  return (
    <div className="space-y-6">
      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("server.maintenance.loadFailed")}</p>
        </div>
      )}

      {isLoading && !data && (
        <div className="space-y-4">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-40 w-full" />
        </div>
      )}

      {data && <MaintenanceContent data={data} />}

      {/* Блок планов обслуживания (MLC-217) — под таблицей свежести бэкапов, отдельный запрос. */}
      <MaintenancePlansSection />
    </div>
  );
}

function MaintenanceContent({ data }: { data: BackupFreshness }) {
  const { t } = useTranslation();

  // Деградация пробы — честная плашка вместо пустой таблицы (нет прав на backupset / нет SQL).
  if (data.status !== "Ok") {
    const message =
      data.status === "PermissionDenied"
        ? t("server.maintenance.permissionDenied")
        : t("server.maintenance.unavailable");
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t("server.maintenance.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-status-danger text-sm">{message}</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("server.maintenance.title")}</CardTitle>
      </CardHeader>
      <CardContent>
        {data.databases.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t("server.maintenance.empty")}</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("server.maintenance.columns.database")}</TableHead>
                <TableHead>{t("server.maintenance.columns.lastFull")}</TableHead>
                <TableHead>{t("server.maintenance.columns.lastDiff")}</TableHead>
                <TableHead>{t("server.maintenance.columns.lastLog")}</TableHead>
                <TableHead>{t("server.maintenance.columns.freshness")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.databases.map((db) => (
                <BackupRow key={db.databaseName} db={db} />
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

function BackupRow({ db }: { db: DatabaseBackupFreshness }) {
  const { t } = useTranslation();
  return (
    <TableRow>
      <TableCell className="font-medium">{db.databaseName}</TableCell>
      <TableCell>{fmt(db.lastFullUtc)}</TableCell>
      <TableCell>{fmt(db.lastDiffUtc)}</TableCell>
      <TableCell>{fmt(db.lastLogUtc)}</TableCell>
      <TableCell>
        <StatusBadge variant={db.isStale ? "danger" : "success"}>
          {db.isStale ? t("server.maintenance.stale") : t("server.maintenance.fresh")}
        </StatusBadge>
      </TableCell>
    </TableRow>
  );
}

// ── Планы обслуживания (MLC-217) ────────────────────────────────────────────────

/**
 * Блок планов обслуживания SQL (MLC-217): под-планы каждого плана с последним прогоном
 * (успех/провал/просрочен/норма), длительностью и развёрткой по задачам (что именно упало).
 * Помечает «по расписанию / по запросу». Деградация (нет прав / SQL Agent недоступен на
 * Express / SQL недоступен) — честной плашкой со статусом, не падением. Own-query — лениво,
 * как и сама вкладка.
 */
function MaintenancePlansSection() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useMaintenancePlans();

  if (isError) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t("server.maintenance.plans.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-status-danger text-sm">{t("server.maintenance.plans.loadFailed")}</p>
        </CardContent>
      </Card>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  return data ? <MaintenancePlansContent data={data} /> : null;
}

function MaintenancePlansContent({ data }: { data: MaintenancePlans }) {
  const { t } = useTranslation();

  // Деградация пробы — честная плашка вместо пустого блока.
  if (data.status !== "Ok") {
    const message =
      data.status === "AgentUnavailable"
        ? t("server.maintenance.plans.agentUnavailable")
        : data.status === "PermissionDenied"
          ? t("server.maintenance.plans.permissionDenied")
          : t("server.maintenance.plans.unavailable");
    return (
      <Card>
        <CardHeader>
          <CardTitle>{t("server.maintenance.plans.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-status-danger text-sm">{message}</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("server.maintenance.plans.title")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-6">
        {data.plans.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t("server.maintenance.plans.empty")}</p>
        ) : (
          data.plans.map((plan) => <PlanBlock key={plan.name} plan={plan} />)
        )}
      </CardContent>
    </Card>
  );
}

function PlanBlock({ plan }: { plan: MaintenancePlan }) {
  return (
    <div className="space-y-2">
      <h4 className="text-sm font-semibold">{plan.name}</h4>
      <div className="grid gap-2">
        {plan.subplans.map((sp) => (
          <SubplanRow key={sp.name} subplan={sp} />
        ))}
      </div>
    </div>
  );
}

function SubplanRow({ subplan }: { subplan: MaintenanceSubplan }) {
  const { t } = useTranslation();
  const hasTasks = subplan.tasks.length > 0;

  const summary = (
    <div className="flex flex-wrap items-center gap-3">
      <span className="font-medium">{subplan.name}</span>
      <OutcomeBadge outcome={subplan.outcome} />
      <span className="text-muted-foreground text-xs">
        {subplan.hasSchedule
          ? t("server.maintenance.plans.scheduled")
          : t("server.maintenance.plans.onDemand")}
      </span>
      {subplan.lastRunUtc && (
        <span className="text-muted-foreground text-xs">
          {t("server.maintenance.plans.lastRun", { value: fmt(subplan.lastRunUtc) })}
        </span>
      )}
      {subplan.durationSeconds != null && (
        <span className="text-muted-foreground text-xs">
          {t("server.maintenance.plans.duration", {
            value: formatDuration(subplan.durationSeconds),
          })}
        </span>
      )}
    </div>
  );

  // Развёртка по задачам (что именно упало) — только если есть детализация шагов.
  if (!hasTasks) {
    return <div className="rounded-md border p-3">{summary}</div>;
  }

  return (
    <details className="rounded-md border p-3">
      <summary className="cursor-pointer list-none [&::-webkit-details-marker]:hidden">
        {summary}
      </summary>
      <ul className="mt-3 space-y-1.5 border-t pt-3">
        {subplan.tasks.map((task, i) => (
          <li key={i} className="flex items-center gap-2 text-sm">
            <StatusBadge variant={task.succeeded ? "success" : "danger"}>
              {task.succeeded
                ? t("server.maintenance.plans.taskOk")
                : t("server.maintenance.plans.taskFailed")}
            </StatusBadge>
            <span>{task.detail}</span>
          </li>
        ))}
      </ul>
    </details>
  );
}

// Бейдж итога прогона под-плана. Failed/Overdue — danger (алертный), Succeeded — success,
// NeverRun — neutral (ручной под-план не запускался — это норма), прочее — neutral.
function OutcomeBadge({ outcome }: { outcome: string }) {
  const { t } = useTranslation();
  const variant = alertingOutcomes.has(outcome)
    ? "danger"
    : outcome === "Succeeded"
      ? "success"
      : "neutral";
  return (
    <StatusBadge variant={variant}>
      {t(`server.maintenance.plans.outcome.${outcome}`, { defaultValue: outcome })}
    </StatusBadge>
  );
}

// Длительность в секундах → человекочитаемо (с / мин).
function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${Math.round(seconds)} с`;
  }
  const minutes = Math.floor(seconds / 60);
  const rest = Math.round(seconds % 60);
  return rest === 0 ? `${minutes} мин` : `${minutes} мин ${rest} с`;
}

// ISO-8601 UTC → локальная дата-время оператора (как BackupsDialog); null → «—».
function fmt(iso: string | null): string {
  return iso ? format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru }) : "—";
}
