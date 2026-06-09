import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { RecordingExportMenu } from "./export/RecordingExportMenu";
import { OneCProcessesTable } from "./OneCProcessesTable";
import { OneCSessionsTable } from "./OneCSessionsTable";
import { RecordingHostChart } from "./RecordingHostChart";
import {
  aggregateOneCProcesses,
  aggregateOneCSessions,
  aggregateSqlRequests,
  hasOneCData,
  hasSqlData,
  lastOneCCapturedAt,
  RECORDING_STATUS_VARIANT,
} from "./recordingAggregation";
import { SqlActiveRequestsTable } from "./SqlActiveRequestsTable";
import { SqlDatabaseLoadTable } from "./SqlDatabaseLoadTable";
import { buildAttributionMap } from "./sqlLoad";
import { useRecordingDetail } from "./useRecordings";

interface RecordingDetailDialogProps {
  recordingId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function fmt(iso: string): string {
  return format(new Date(iso), "dd.MM.yyyy HH:mm:ss", { locale: ru });
}

/**
 * Просмотр записи быстродействия (MLC-071): метаданные расследования + график host-метрик во
 * времени + топ-виновники 1С/SQL за период + экспорт. Реюзает live-таблицы 067/069 на свёрнутых
 * за период срезах (`recordingAggregation`). Запись хранит только snapshot DMV-пробы без атрибуции
 * база→клиент (она живёт лишь в live-эндпоинте), поэтому SQL-таблицы получают пустую карту.
 */
export function RecordingDetailDialog({
  recordingId,
  open,
  onOpenChange,
}: RecordingDetailDialogProps) {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useRecordingDetail(open ? recordingId : null);

  const samples = useMemo(() => data?.samples ?? [], [data]);
  const oneCSessions = useMemo(() => aggregateOneCSessions(samples), [samples]);
  const oneCProcesses = useMemo(() => aggregateOneCProcesses(samples), [samples]);
  const sqlRequests = useMemo(() => aggregateSqlRequests(samples), [samples]);
  const emptyAttribution = useMemo(() => buildAttributionMap([]), []);
  const capturedAt = lastOneCCapturedAt(samples);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] gap-0 overflow-y-auto sm:max-w-4xl">
        <DialogHeader className="pr-8">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <DialogTitle>{t("performance.recording.detail.title")}</DialogTitle>
            {data && <RecordingExportMenu detail={data} />}
          </div>
          <DialogDescription asChild>
            {data ? (
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
                <StatusBadge variant={RECORDING_STATUS_VARIANT[data.recording.status]}>
                  {t(`performance.recording.status.${data.recording.status}`)}
                </StatusBadge>
                <span>
                  {fmt(data.recording.startedAtUtc)}
                  {" — "}
                  {data.recording.stoppedAtUtc ? fmt(data.recording.stoppedAtUtc) : "…"}
                </span>
                <span>
                  {t("performance.recording.fields.startedBy")}: {data.recording.startedBy}
                </span>
                <span>
                  {t("performance.recording.fields.samples")}: {data.recording.sampleCount}
                </span>
              </div>
            ) : (
              <span>{t("performance.recording.detail.subtitle")}</span>
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="mt-4 space-y-6">
          {isError && (
            <p className="text-muted-foreground text-sm">
              {t("performance.recording.errors.loadFailed")}
            </p>
          )}

          {isLoading && !data ? (
            <Skeleton className="h-72 w-full" />
          ) : data ? (
            samples.length === 0 ? (
              <p className="text-muted-foreground text-sm">
                {t("performance.recording.detail.noSamples")}
              </p>
            ) : (
              <>
                <section className="space-y-2">
                  <h3 className="text-sm font-medium">
                    {t("performance.recording.chart.heading")}
                  </h3>
                  <RecordingHostChart samples={samples} />
                </section>

                {hasOneCData(samples) && capturedAt && (
                  <section className="space-y-2">
                    <h3 className="text-sm font-medium">
                      {t("performance.recording.culprits.onec")}
                    </h3>
                    <OneCSessionsTable sessions={oneCSessions} capturedAtUtc={capturedAt} />
                    <OneCProcessesTable processes={oneCProcesses} />
                  </section>
                )}

                {hasSqlData(samples) && (
                  <section className="space-y-2">
                    <h3 className="text-sm font-medium">
                      {t("performance.recording.culprits.sql")}
                    </h3>
                    <SqlActiveRequestsTable
                      requests={sqlRequests}
                      attributionMap={emptyAttribution}
                    />
                    <SqlDatabaseLoadTable
                      requests={sqlRequests}
                      attributionMap={emptyAttribution}
                    />
                  </section>
                )}
              </>
            )
          ) : null}
        </div>
      </DialogContent>
    </Dialog>
  );
}
