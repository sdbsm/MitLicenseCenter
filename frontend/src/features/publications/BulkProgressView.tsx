import {
  CheckCircle2Icon,
  CircleIcon,
  Loader2Icon,
  MinusCircleIcon,
  XCircleIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Progress } from "@/components/ui/progress";
import { cn } from "@/lib/utils";
import type { BulkItemState, BulkItemStatus, BulkSummary } from "./useBulkOperation";

interface BulkProgressViewProps {
  states: BulkItemState[];
  summary: BulkSummary;
  isRunning: boolean;
}

const STATUS_ICON: Record<BulkItemStatus, typeof CircleIcon> = {
  pending: CircleIcon,
  running: Loader2Icon,
  ok: CheckCircle2Icon,
  error: XCircleIcon,
  skipped: MinusCircleIcon,
};

const STATUS_COLOR: Record<BulkItemStatus, string> = {
  pending: "text-muted-foreground",
  running: "text-primary animate-spin",
  ok: "text-emerald-600 dark:text-emerald-400",
  error: "text-destructive",
  skipped: "text-muted-foreground",
};

// MLC-046: прогресс пачки — бар + сводка + построчный статус с деталью ошибки.
export function BulkProgressView({ states, summary, isRunning }: BulkProgressViewProps) {
  const { t } = useTranslation();
  const percent = summary.total === 0 ? 0 : Math.round((summary.done / summary.total) * 100);

  return (
    <div className="space-y-3">
      <div className="space-y-1.5">
        <Progress value={percent} />
        <p className="text-muted-foreground text-sm">
          {isRunning
            ? t("publications.bulk.progress.running", {
                done: summary.done,
                total: summary.total,
              })
            : t("publications.bulk.progress.summary", {
                ok: summary.ok,
                error: summary.error,
                skipped: summary.skipped,
              })}
        </p>
      </div>

      <ul className="max-h-64 space-y-1 overflow-y-auto rounded-md border p-2">
        {states.map((s) => {
          const Icon = STATUS_ICON[s.status];
          return (
            <li key={s.id} className="flex items-start gap-2 text-sm">
              <Icon className={cn("mt-0.5 size-4 shrink-0", STATUS_COLOR[s.status])} />
              <div className="min-w-0">
                <div className="truncate font-mono text-xs">{s.label}</div>
                {s.status === "error" && s.error && (
                  <div className="text-destructive text-xs whitespace-pre-line">{s.error}</div>
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
