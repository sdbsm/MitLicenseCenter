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

// ISO-8601 UTC → локальная дата-время оператора (как BackupsDialog); null → «—».
function fmt(iso: string | null): string {
  return iso ? format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru }) : "—";
}
