import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { AuditFilters } from "@/features/audit/types";
import { useAuditLog } from "@/features/audit/useAuditLog";

// MLC-186d — лента «Свежая активность» на «Обзоре»: последние 5 записей журнала
// аудита. /audit доступен и Viewer'у (без role-gate), поэтому RBAC здесь не нужен.
// Фильтр мемоизируем один раз (пустые зависимости): новый объект каждый рендер дал
// бы новый react-query-key и бесконечный рефетч. pageSize ограничен типом
// AuditPageSize ([25,50,100]) — запрашиваем 25 и режем до 5 на клиенте.
const ACTIVITY_COUNT = 5;

export function RecentActivityCard() {
  const { t } = useTranslation();

  const filters = useMemo<AuditFilters>(
    () => ({
      actionType: null,
      tenantId: null,
      from: null,
      to: null,
      search: null,
      initiator: null,
      page: 1,
      pageSize: 25,
    }),
    []
  );

  const { data, isLoading, isError } = useAuditLog(filters);
  const items = data?.items.slice(0, ACTIVITY_COUNT) ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0">
        <CardTitle>{t("dashboard.activity.title")}</CardTitle>
        <Link to="/audit" className="text-primary text-sm underline-offset-2 hover:underline">
          {t("dashboard.activity.showAll")}
        </Link>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-3">
            {Array.from({ length: ACTIVITY_COUNT }).map((_, idx) => (
              <Skeleton key={idx} className="h-9 w-full" />
            ))}
          </div>
        ) : isError ? (
          <p className="text-muted-foreground text-sm">{t("dashboard.refresh.error")}</p>
        ) : items.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t("dashboard.activity.empty")}</p>
        ) : (
          <ul className="space-y-1.5">
            {items.map((entry) => (
              <li key={entry.id} className="flex items-center gap-2.5 px-2 py-1.5">
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{t(`audit.actions.${entry.actionType}`)}</p>
                  <p className="text-muted-foreground truncate text-xs">{entry.description}</p>
                </div>
                <RelativeTime value={entry.timestamp} className="shrink-0 text-xs" />
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
