import { useTranslation } from "react-i18next";
import { Skeleton } from "@/components/ui/skeleton";

interface SessionsLicenseBandProps {
  consumed: number;
  limit: number;
  free: number;
  active: number;
  isLoading: boolean;
}

/**
 * Лицензионный банд проекции «Живые сеансы» (MLC-196a): тихая плотная строка KPI
 * host-уровня «Использовано / лимит · Свободно · Активных». Цифры — из
 * `useDashboardSummary` (`/dashboard/summary`, без нового контракта). Без крупных
 * кнопок и акцента — только числа (tabular/mono по дизайн-системе Фазы 0).
 */
export function SessionsLicenseBand({
  consumed,
  limit,
  free,
  active,
  isLoading,
}: SessionsLicenseBandProps) {
  const { t } = useTranslation();

  if (isLoading) {
    return (
      <div className="bg-card flex flex-wrap items-center gap-x-8 gap-y-2 rounded-md border px-4 py-3">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="h-5 w-28" />
        <Skeleton className="h-5 w-28" />
      </div>
    );
  }

  return (
    <div className="bg-card flex flex-wrap items-center gap-x-8 gap-y-2 rounded-md border px-4 py-3 text-sm">
      <BandStat label={t("sessions.band.consumed")} value={`${consumed} / ${limit}`} />
      <BandStat label={t("sessions.band.free")} value={String(free)} />
      <BandStat label={t("sessions.band.active")} value={String(active)} />
    </div>
  );
}

function BandStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline gap-2">
      <span className="text-muted-foreground text-xs tracking-wide uppercase">{label}</span>
      <span className="font-mono font-medium tabular-nums">{value}</span>
    </div>
  );
}
