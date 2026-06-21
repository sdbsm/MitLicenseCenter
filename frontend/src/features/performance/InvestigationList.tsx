import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { CircleIcon, FolderSearchIcon, Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import { useInvestigations } from "@/features/investigations/useInvestigations";
import {
  INVESTIGATION_STATUS_VARIANT,
  fmtDate,
} from "@/features/investigations/investigationUtils";
import type {
  InvestigationScenario,
  InvestigationStatus,
  InvestigationSummary,
} from "@/features/investigations/types";
import { DeleteInvestigationDialog } from "./DeleteInvestigationDialog";

/**
 * Список дел расследования — экран 5 (MLC-243, ADR-57, спека §Экран 5).
 *
 * Дефолтный вид режима «Расследование», когда нет активного сбора.
 * Баннер активного сбора сверху (если есть Collecting/Analyzing) → onShowProgress.
 * Кнопка «+ Новое расследование» → onNewInvestigation.
 * Клик по строке → onSelectInvestigation(id).
 * Фильтры клиентские (по загруженной странице — без новых эндпоинтов).
 */

const SCENARIO_OPTIONS: Array<InvestigationScenario | ""> = [
  "",
  "Locks",
  "SlowQueries",
  "Exceptions",
  "GeneralSlow",
  "DbmsLocks",
];

const STATUS_OPTIONS: Array<InvestigationStatus | ""> = [
  "",
  "Collecting",
  "Analyzing",
  "Completed",
  "Interrupted",
  "Failed",
];

interface InvestigationListProps {
  onNewInvestigation: () => void;
  onSelectInvestigation: (id: string) => void;
  onShowProgress: () => void;
}

function fmtPeriod(startedAtUtc: string, stoppedAtUtc: string | null): string {
  const start = fmtDate(startedAtUtc);
  if (!stoppedAtUtc) return start;
  // Если дата окончания совпадает с датой начала — показываем только время окончания
  const startDay = format(new Date(startedAtUtc), "dd.MM.yyyy", { locale: ru });
  const stopDay = format(new Date(stoppedAtUtc), "dd.MM.yyyy", { locale: ru });
  const stopTime = format(new Date(stoppedAtUtc), "HH:mm", { locale: ru });
  if (startDay === stopDay) {
    return `${start} – ${stopTime}`;
  }
  return `${start} – ${fmtDate(stoppedAtUtc)}`;
}

export function InvestigationList({
  onNewInvestigation,
  onSelectInvestigation,
  onShowProgress,
}: InvestigationListProps) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const { data, isLoading } = useInvestigations();

  // Список инфобаз для разрешения infobaseId → имя
  const { data: infobasesData } = useInfobases(null, null, false, 1, 100);
  const infobaseMap = useMemo(() => {
    const m = new Map<string, string>();
    for (const ib of infobasesData?.items ?? []) {
      m.set(ib.id, ib.name);
    }
    return m;
  }, [infobasesData]);

  const items: InvestigationSummary[] = data?.items ?? [];

  // Активное дело (Collecting / Analyzing) — инвариант: одно за раз
  const activeSummary = useMemo(
    () => items.find((inv) => inv.status === "Collecting" || inv.status === "Analyzing") ?? null,
    [items]
  );

  // Клиентские фильтры (по загруженной странице)
  const [scenarioFilter, setScenarioFilter] = useState<InvestigationScenario | "">("");
  const [statusFilter, setStatusFilter] = useState<InvestigationStatus | "">("");

  const filtered = useMemo(() => {
    return items.filter((inv) => {
      if (scenarioFilter && inv.scenario !== scenarioFilter) return false;
      if (statusFilter && inv.status !== statusFilter) return false;
      return true;
    });
  }, [items, scenarioFilter, statusFilter]);

  // Удаление
  const [deletingId, setDeletingId] = useState<string | null>(null);

  function resolveScope(inv: InvestigationSummary): string {
    if (inv.infobaseId) {
      return infobaseMap.get(inv.infobaseId) ?? inv.infobaseId;
    }
    return t("investigations.list.scopeAll");
  }

  return (
    <div className="space-y-4">
      {/* Заголовок + кнопка «+ Новое расследование» */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold">{t("investigations.title")}</h2>
        <Button size="sm" onClick={onNewInvestigation}>
          {t("investigations.list.newInvestigation")}
        </Button>
      </div>

      {/* Баннер активного сбора */}
      {activeSummary && (
        <div className="bg-muted/60 flex items-center gap-3 rounded-lg px-4 py-2">
          <CircleIcon
            className="size-3 shrink-0 animate-pulse fill-rose-600 text-rose-600 dark:fill-rose-400 dark:text-rose-400"
            aria-hidden="true"
          />
          <span className="text-sm font-medium">
            {t("investigations.list.activeBanner", {
              scenario: t(`investigations.scenario.${activeSummary.scenario}`),
            })}
          </span>
          <button
            type="button"
            className="text-primary ml-auto text-sm underline underline-offset-2 hover:no-underline"
            onClick={onShowProgress}
          >
            {t("investigations.list.activeBannerLink")}
          </button>
        </div>
      )}

      {/* Фильтры */}
      <div className="flex flex-wrap gap-2">
        <Select
          value={scenarioFilter || "all"}
          onValueChange={(v) => setScenarioFilter(v === "all" ? "" : (v as InvestigationScenario))}
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder={t("investigations.list.filter.allScenarios")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("investigations.list.filter.allScenarios")}</SelectItem>
            {SCENARIO_OPTIONS.filter(Boolean).map((s) => (
              <SelectItem key={s} value={s}>
                {t(`investigations.scenario.${s}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={statusFilter || "all"}
          onValueChange={(v) => setStatusFilter(v === "all" ? "" : (v as InvestigationStatus))}
        >
          <SelectTrigger className="w-44">
            <SelectValue placeholder={t("investigations.list.filter.allStatuses")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("investigations.list.filter.allStatuses")}</SelectItem>
            {STATUS_OPTIONS.filter(Boolean).map((s) => (
              <SelectItem key={s} value={s}>
                {t(`investigations.status.${s}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Таблица */}
      {isLoading && !data ? (
        <Skeleton className="h-40 w-full" />
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
          <FolderSearchIcon className="text-muted-foreground size-8" />
          <div className="space-y-1">
            <p className="font-medium">{t("investigations.list.empty.title")}</p>
            <p className="text-muted-foreground text-sm">{t("investigations.list.empty.hint")}</p>
          </div>
        </div>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-24">{t("investigations.list.cols.number")}</TableHead>
                <TableHead>{t("investigations.list.cols.period")}</TableHead>
                <TableHead>{t("investigations.list.cols.scenario")}</TableHead>
                <TableHead>{t("investigations.list.cols.target")}</TableHead>
                <TableHead className="w-32">{t("investigations.list.cols.status")}</TableHead>
                <TableHead>{t("investigations.list.cols.startedBy")}</TableHead>
                <TableHead className="w-20 text-right">
                  {t("investigations.list.cols.findings")}
                </TableHead>
                {isAdmin && <TableHead className="w-10" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((inv) => (
                <TableRow
                  key={inv.id}
                  className="cursor-pointer"
                  onClick={() => onSelectInvestigation(inv.id)}
                >
                  {/* № — первые 8 символов id */}
                  <TableCell className="text-muted-foreground font-mono text-xs">
                    {inv.id.slice(0, 8)}
                  </TableCell>
                  {/* Период */}
                  <TableCell className="text-sm tabular-nums">
                    {fmtPeriod(inv.startedAtUtc, inv.stoppedAtUtc)}
                  </TableCell>
                  {/* Сценарий */}
                  <TableCell className="text-sm">
                    {t(`investigations.scenario.${inv.scenario}`)}
                  </TableCell>
                  {/* Арендатор / ИБ */}
                  <TableCell className="text-sm">{resolveScope(inv)}</TableCell>
                  {/* Статус */}
                  <TableCell>
                    <StatusBadge variant={INVESTIGATION_STATUS_VARIANT[inv.status]}>
                      {t(`investigations.status.${inv.status}`)}
                    </StatusBadge>
                  </TableCell>
                  {/* Кто запустил */}
                  <TableCell className="text-muted-foreground text-sm">{inv.startedBy}</TableCell>
                  {/* Число находок */}
                  <TableCell className="text-right tabular-nums">{inv.findingsCount}</TableCell>
                  {/* Удаление (только Admin) */}
                  {isAdmin && (
                    <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-8"
                        aria-label={t("investigations.list.delete.title")}
                        onClick={() => setDeletingId(inv.id)}
                      >
                        <Trash2Icon className="size-4" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <DeleteInvestigationDialog
        investigationId={deletingId}
        open={deletingId !== null}
        onOpenChange={(open) => {
          if (!open) setDeletingId(null);
        }}
      />
    </div>
  );
}
