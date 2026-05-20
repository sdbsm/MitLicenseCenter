import { useEffect, useState } from "react";
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
  const diffSec = Math.max(0, Math.floor((now - date.getTime()) / 1_000));

  const colorClass = isError
    ? "text-destructive"
    : diffSec > thresholdAmberSec
      ? "text-amber-600 dark:text-amber-400"
      : "text-muted-foreground";

  return <span className={cn(colorClass, className)}>{formatRelative(diffSec)}</span>;
}
