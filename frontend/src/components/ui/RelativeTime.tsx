import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useEffect, useState } from "react";
import i18n from "@/i18n";
import { cn } from "@/lib/utils";

interface RelativeTimeProps {
  value: string | Date;
  thresholdAmberSec?: number;
  isError?: boolean;
  className?: string;
}

const rtf = new Intl.RelativeTimeFormat("ru", { numeric: "auto" });

function formatRelative(diffSec: number): string {
  if (diffSec < 60) {
    return rtf.format(-diffSec, "second");
  }
  return rtf.format(-Math.floor(diffSec / 60), "minute");
}

// MLC-148: «холодный старт» серверных снапшотов отдаёт CapturedAtUtc = DateTime.MinValue
// (0001-01-01), который без защиты рендерился как «~2000 лет назад». Любая дата до эпохи
// Unix (1970) трактуется как «значения ещё нет». Защита холистическая — RelativeTime
// используется на /sessions, /performance, /dashboard, баннерах инфобаз.
function isUnsetTimestamp(date: Date): boolean {
  const ms = date.getTime();
  return Number.isNaN(ms) || ms <= 0;
}

export function RelativeTime({
  value,
  thresholdAmberSec = 60,
  isError = false,
  className,
}: RelativeTimeProps) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1_000);
    return () => clearInterval(id);
  }, []);

  const date = typeof value === "string" ? new Date(value) : value;

  if (isUnsetTimestamp(date)) {
    const label = i18n.t("common.notUpdatedYet");
    return (
      <span className={cn("text-muted-foreground", className)}>{label}</span>
    );
  }

  const diffSec = Math.max(0, Math.floor((now - date.getTime()) / 1_000));

  const colorClass = isError
    ? "text-destructive"
    : diffSec > thresholdAmberSec
      ? "text-amber-600 dark:text-amber-400"
      : "text-muted-foreground";

  // Канон 06_UI_DESIGN.md §8: индикатор свежести показывает точную метку времени
  // в тултипе при наведении.
  const title = format(date, "dd.MM.yyyy HH:mm:ss", { locale: ru });

  return (
    <span title={title} className={cn(colorClass, className)}>
      {formatRelative(diffSec)}
    </span>
  );
}
