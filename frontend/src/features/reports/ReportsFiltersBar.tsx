import { format } from "date-fns";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { monthToRange, shiftMonth } from "./reportsUrlState";
import type { ReportsFilters } from "./types";

interface ReportsFiltersBarProps {
  filters: ReportsFilters;
  onChange: (next: ReportsFilters) => void;
}

/** Фильтр периода (общий на сводку и детализацию). Без указанных границ сервер
 *  берёт последние 7 дней; ширину >31 дня кламп двигает сам (эффективный диапазон —
 *  в подписи сводки). Помесячный выбор «‹ месяц ›» заполняет те же from/to границами
 *  месяца (целый месяц < 31 дня → кламп не триггерит). Образец — AuditFiltersBar. */
export function ReportsFiltersBar({ filters, onChange }: ReportsFiltersBarProps) {
  const { t } = useTranslation();

  const update = (patch: Partial<ReportsFilters>) => {
    onChange({ ...filters, ...patch });
  };

  const hasPeriod = filters.from !== null || filters.to !== null;

  // Текущий выбранный месяц «YYYY-MM»: из начала периода, иначе календарный (пустой
  // фильтр). new Date() в app-рантайме допустим (не в тестируемом хелпере).
  const currentMonth = filters.from?.slice(0, 7) ?? format(new Date(), "yyyy-MM");
  const setMonth = (ym: string) => update(monthToRange(ym));

  return (
    <div className="bg-muted/30 flex flex-wrap items-end gap-3 rounded-md border p-3">
      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="reports-from">
          {t("reports.filters.from")}
        </Label>
        <Input
          id="reports-from"
          type="date"
          className="w-40"
          value={filters.from ?? ""}
          onChange={(e) => update({ from: e.target.value || null })}
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="reports-to">
          {t("reports.filters.to")}
        </Label>
        <Input
          id="reports-to"
          type="date"
          className="w-40"
          value={filters.to ?? ""}
          onChange={(e) => update({ to: e.target.value || null })}
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="reports-month">
          {t("reports.filters.month")}
        </Label>
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon"
            aria-label={t("reports.filters.monthPrev")}
            onClick={() => setMonth(shiftMonth(currentMonth, -1))}
          >
            <ChevronLeft className="size-4" />
          </Button>
          <Input
            id="reports-month"
            type="month"
            className="w-36"
            value={currentMonth}
            onChange={(e) => {
              if (e.target.value) setMonth(e.target.value);
            }}
          />
          <Button
            variant="outline"
            size="icon"
            aria-label={t("reports.filters.monthNext")}
            onClick={() => setMonth(shiftMonth(currentMonth, 1))}
          >
            <ChevronRight className="size-4" />
          </Button>
        </div>
      </div>

      {hasPeriod && (
        <Button variant="ghost" size="sm" onClick={() => update({ from: null, to: null })}>
          {t("common.reset")}
        </Button>
      )}
    </div>
  );
}
