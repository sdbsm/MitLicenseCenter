import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { EyeIcon, MoreHorizontalIcon, Trash2Icon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StatusBadge } from "@/components/ui/StatusBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { RECORDING_STATUS_VARIANT } from "./recordingAggregation";
import type { RecordingSummary } from "./types";

interface RecordingsTableProps {
  recordings: RecordingSummary[];
  isAdmin: boolean;
  onView: (recording: RecordingSummary) => void;
  onDelete: (recording: RecordingSummary) => void;
}

function fmt(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru });
}

/**
 * Список расследований (MLC-071): время старта/окончания, статус, кто запустил, причина стопа,
 * число сэмплов. Любой ряд открывается на просмотр (Viewer); удаление — только Admin (Active
 * удалить нельзя, бэкенд вернёт 409). Свежие сверху (порядок задаёт бэкенд).
 */
export function RecordingsTable({ recordings, isAdmin, onView, onDelete }: RecordingsTableProps) {
  const { t } = useTranslation();

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("performance.recording.fields.started")}</TableHead>
            <TableHead>{t("performance.recording.fields.stopped")}</TableHead>
            <TableHead className="w-28">{t("performance.recording.fields.status")}</TableHead>
            <TableHead>{t("performance.recording.fields.startedBy")}</TableHead>
            <TableHead className="w-20 text-right">
              {t("performance.recording.fields.samples")}
            </TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {recordings.map((r) => (
            <TableRow key={r.id} className="cursor-pointer" onClick={() => onView(r)}>
              <TableCell className="tabular-nums">{fmt(r.startedAtUtc)}</TableCell>
              <TableCell className="text-muted-foreground tabular-nums">
                {r.stoppedAtUtc ? fmt(r.stoppedAtUtc) : "—"}
              </TableCell>
              <TableCell>
                <StatusBadge variant={RECORDING_STATUS_VARIANT[r.status]}>
                  {t(`performance.recording.status.${r.status}`)}
                </StatusBadge>
                {r.stopReason && (
                  <span className="text-muted-foreground ml-2 text-xs">
                    {t(`performance.recording.stopReason.${r.stopReason}`)}
                  </span>
                )}
              </TableCell>
              <TableCell className="text-muted-foreground">{r.startedBy}</TableCell>
              <TableCell className="text-right tabular-nums">{r.sampleCount}</TableCell>
              <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="size-8">
                      <MoreHorizontalIcon className="size-4" />
                      <span className="sr-only">{t("common.details")}</span>
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onSelect={() => onView(r)}>
                      <EyeIcon className="size-4" />
                      {t("performance.recording.actions.view")}
                    </DropdownMenuItem>
                    {isAdmin && (
                      <DropdownMenuItem
                        variant="destructive"
                        disabled={r.status === "Active"}
                        onSelect={() => onDelete(r)}
                      >
                        <Trash2Icon className="size-4" />
                        {t("performance.recording.actions.delete")}
                      </DropdownMenuItem>
                    )}
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
