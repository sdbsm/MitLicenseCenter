import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import {
  AlertTriangleIcon,
  CheckCircleIcon,
  ChevronRightIcon,
  HardDriveIcon,
  InfoIcon,
  LayersIcon,
  PlugZapIcon,
  UsersIcon,
  type LucideIcon,
} from "lucide-react";
import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useMe } from "@/features/auth/useAuth";
import { cn } from "@/lib/utils";
import type { DashboardSummaryResponse } from "./types";
import { useDashboardAlerts } from "./useDashboardAlerts";

// MLC-186b — виджет «Требует внимания»: единый список actionable-сигналов на «Обзоре».
// Источника два: серверный агрегат /dashboard/alerts (квоты, дрейф кластера, диск
// бэкапов) и summary с «Обзора» (RAS-health + доступность факта лицензий), который
// уже грузит DashboardPage — передаём его пропсом, чтобы не дублировать запрос.
//
// Принципы:
//  - строка показывается ТОЛЬКО когда её сигнал активен (danger перед warning);
//  - переход даётся ссылкой, только если цель доступна текущей роли (часть целей —
//    Admin-only: /settings); для не-Admin такие строки рендерятся без ссылки;
//  - дрейф кластера приходит null для не-Admin (Admin-only на бэкенде) → строк нет;
//  - пусто и данные загружены → одна success-строка «Всё в порядке».

interface AlertRow {
  key: string;
  icon: LucideIcon;
  variant: StatusBadgeVariant;
  text: string;
  to?: string;
}

interface AttentionWidgetProps {
  summary: DashboardSummaryResponse | undefined;
}

export function AttentionWidget({ summary }: AttentionWidgetProps) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;
  const { data: alerts, isLoading } = useDashboardAlerts();

  const rows: AlertRow[] = [];

  if (alerts) {
    // Квоты — три ФАКТИЧЕСКИХ бакета (MLC-193, зеркало lib/quota.ts): превышение и «лимит
    // достигнут» оба danger-цвет, «близко» — warning. Порядок: превышение → достигнут → близко.
    if (alerts.quotaExceeded > 0) {
      rows.push({
        key: "quota-exceeded",
        icon: UsersIcon,
        variant: "danger",
        text: t("dashboard.alerts.quotaExceeded", { count: alerts.quotaExceeded }),
        to: "/tenants",
      });
    }
    if (alerts.quotaAtLimit > 0) {
      rows.push({
        key: "quota-at-limit",
        icon: UsersIcon,
        variant: "danger",
        text: t("dashboard.alerts.quotaAtLimit", { count: alerts.quotaAtLimit }),
        to: "/tenants",
      });
    }
    if (alerts.quotaNearLimit > 0) {
      rows.push({
        key: "quota-near-limit",
        icon: UsersIcon,
        variant: "warning",
        text: t("dashboard.alerts.quotaNearLimit", { count: alerts.quotaNearLimit }),
        to: "/tenants",
      });
    }

    // Дрейф кластера — только когда агрегат доступен (clusterDrift !== null для Admin
    // и available === true). Для не-Admin clusterDrift === null → строк нет.
    const drift = alerts.clusterDrift;
    if (drift?.available === true) {
      if ((drift.basesNotInCluster ?? 0) > 0) {
        rows.push({
          key: "drift-missing",
          icon: LayersIcon,
          variant: "danger",
          text: t("dashboard.alerts.basesNotInCluster", { count: drift.basesNotInCluster ?? 0 }),
          to: "/infobases",
        });
      }
      if ((drift.unassignedBases ?? 0) > 0) {
        rows.push({
          key: "drift-unassigned",
          icon: LayersIcon,
          variant: "warning",
          text: t("dashboard.alerts.unassignedBases", { count: drift.unassignedBases ?? 0 }),
          to: "/infobases",
        });
      }
    }

    // Мало места на диске бэкапов — управление бэкапами в «Параметрах» (Admin-only):
    // ссылку даём только Admin, не-Admin видит строку без перехода.
    if (alerts.backupDisk.low === true) {
      rows.push({
        key: "backup-disk",
        icon: HardDriveIcon,
        variant: "warning",
        text: t("dashboard.alerts.backupDiskLow"),
        to: isAdmin ? "/settings" : undefined,
      });
    }
  }

  if (summary) {
    // RAS недоступен: healthy === false И уже была хотя бы одна проверка
    // (lastCheckedAtUtc !== null/undefined) — иначе это «ещё проверяю», не сбой.
    const ras = summary.ras;
    if (!ras.healthy && ras.lastCheckedAtUtc != null) {
      rows.push({
        key: "ras",
        icon: PlugZapIcon,
        variant: "danger",
        text: t("dashboard.alerts.rasDown"),
        to: isAdmin ? "/settings" : undefined,
      });
    }

    // Факт лицензий недоступен — информационный сигнал, без перехода.
    if (summary.licenseFactAvailable === false) {
      rows.push({
        key: "license-fact",
        icon: InfoIcon,
        variant: "info",
        text: t("dashboard.alerts.licenseFactUnavailable"),
      });
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <AlertTriangleIcon className="text-muted-foreground h-4 w-4" aria-hidden />
          {t("dashboard.alerts.title")}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, idx) => (
              <Skeleton key={idx} className="h-9 w-full" />
            ))}
          </div>
        ) : rows.length === 0 ? (
          <AlertLine
            icon={CheckCircleIcon}
            variant="success"
            text={t("dashboard.alerts.allClear")}
          />
        ) : (
          <ul className="space-y-1.5">
            {rows.map((row) => (
              <li key={row.key}>
                <AlertLine icon={row.icon} variant={row.variant} text={row.text} to={row.to} />
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

// Цвет иконки по семантике варианта — зеркалит палитру StatusBadge / quota.ts.
const iconClass: Record<StatusBadgeVariant, string> = {
  success: "text-emerald-600 dark:text-emerald-400",
  warning: "text-amber-600 dark:text-amber-400",
  danger: "text-rose-600 dark:text-rose-400",
  info: "text-sky-600 dark:text-sky-400",
  neutral: "text-muted-foreground",
};

interface AlertLineProps {
  icon: LucideIcon;
  variant: StatusBadgeVariant;
  text: string;
  to?: string;
}

function AlertLine({ icon: Icon, variant, text, to }: AlertLineProps) {
  const body: ReactNode = (
    <>
      <Icon className={cn("h-4 w-4 shrink-0", iconClass[variant])} aria-hidden />
      <span className="min-w-0 flex-1 text-sm">{text}</span>
      {to && <ChevronRightIcon className="text-muted-foreground h-4 w-4 shrink-0" aria-hidden />}
    </>
  );

  if (to) {
    return (
      <Link
        to={to}
        className="hover:bg-muted/50 focus-visible:ring-ring flex items-center gap-2.5 rounded-md px-2 py-1.5 transition-colors focus-visible:ring-2 focus-visible:outline-none"
      >
        {body}
      </Link>
    );
  }

  return <div className="flex items-center gap-2.5 px-2 py-1.5">{body}</div>;
}
