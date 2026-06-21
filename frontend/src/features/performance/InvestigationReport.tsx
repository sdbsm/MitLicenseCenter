import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon, FileTextIcon, DownloadIcon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { useInfobases } from "@/features/infobases/useInfobases";
import {
  useInvestigationReport,
  useInvestigationDetail,
} from "@/features/investigations/useInvestigations";
import { REPORT_SEVERITY_VARIANT, fmtDate } from "@/features/investigations/investigationUtils";
import { downloadBlob } from "@/features/reports/export/downloadBlob";

/**
 * Экран «Отчёт» — документ-вид дела для показа и выгрузки в PDF (MLC-244, ADR-57, спека §Экран 4).
 *
 * Блоки по спеке:
 *   1. Шапка: №, сформирован, период, узел, кто запустил, арендатор/ИБ.
 *      Действия: «Экспорт PDF» + «Открыть дело».
 *   2. Резюме: вердикт + серьёзность из ранжированных items[].
 *      Если items пуст — нейтральное «существенных проблем не выявлено».
 *   3. «Что собрано» (воспроизводимость): из collectionConfig (useInvestigationDetail).
 *      Если collectionConfig нет (историческое дело) — нейтральная подпись.
 *   4. Находки (ранжированы по серьёзности): severity + headline + recommendation + count.
 *   5. Подвал: retention / post-mortem / ИТС.
 *
 * Экспорт PDF: динамический import toInvestigationPdf (jsPDF + Roboto-кириллица),
 * состояние «формирую…», обработка ошибки (toast).
 */

interface InvestigationReportProps {
  investigationId: string;
  /** Открыть дело (экран 3). */
  onOpenDeal: () => void;
  /** Вернуться к списку. */
  onBackToList: () => void;
}

export function InvestigationReport({
  investigationId,
  onOpenDeal,
  onBackToList,
}: InvestigationReportProps) {
  const { t } = useTranslation();
  const [pdfPending, setPdfPending] = useState(false);

  const { data: report, isLoading: reportLoading } = useInvestigationReport(investigationId);
  const { data: detail, isLoading: detailLoading } = useInvestigationDetail(investigationId);
  const { data: infobasesData } = useInfobases(null, null, false, 1, 100);

  const isLoading = reportLoading || detailLoading;

  if (isLoading && (!report || !detail)) {
    return <Skeleton className="h-64 w-full" />;
  }

  if (!report) {
    return (
      <div className="text-muted-foreground py-12 text-center text-sm">{t("common.noData")}</div>
    );
  }

  const { summary, generatedAtUtc, items } = report;
  const collectionConfig = detail?.collectionConfig ?? null;

  // Разрешаем ИБ
  const infobaseName = summary.infobaseId
    ? (infobasesData?.items.find((ib) => ib.id === summary.infobaseId)?.name ?? summary.infobaseId)
    : null;
  const scopeLabel = infobaseName ?? t("investigations.list.scopeAll");

  // Экспорт PDF
  async function handleExportPdf() {
    if (!report) return;
    setPdfPending(true);
    try {
      const { toInvestigationPdf } = await import("@/features/investigations/toInvestigationPdf");
      const blob = await toInvestigationPdf(report, collectionConfig, infobaseName);
      const shortId = summary.id.slice(0, 8);
      downloadBlob(`расследование-${shortId}.pdf`, blob);
    } catch {
      toast.error(t("investigations.report.exportPdfError"));
    } finally {
      setPdfPending(false);
    }
  }

  return (
    <div className="space-y-6">
      {/* Шапка */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-2">
          {/* Кнопки навигации */}
          <div className="flex flex-wrap gap-2">
            <Button variant="ghost" size="sm" onClick={onBackToList} className="-ml-2">
              <ArrowLeftIcon className="size-4" />
              {t("investigations.report.backToList")}
            </Button>
            <Button variant="ghost" size="sm" onClick={onOpenDeal}>
              <FileTextIcon className="size-4" />
              {t("investigations.report.openDeal")}
            </Button>
          </div>

          {/* Мета */}
          <h2 className="text-base font-semibold">
            {t("investigations.report.title")} №{summary.id.slice(0, 8)}
          </h2>
          <dl className="text-muted-foreground grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-sm">
            <dt>{t("investigations.report.generatedAt")}</dt>
            <dd>{fmtDate(generatedAtUtc)}</dd>
            <dt>{t("investigations.report.period")}</dt>
            <dd>
              {fmtDate(summary.startedAtUtc)}
              {summary.stoppedAtUtc && ` – ${fmtDate(summary.stoppedAtUtc)}`}
            </dd>
            <dt>{t("investigations.report.node")}</dt>
            <dd>{t("investigations.report.currentNode")}</dd>
            <dt>{t("investigations.report.startedBy")}</dt>
            <dd>{summary.startedBy}</dd>
            <dt>{t("investigations.report.target")}</dt>
            <dd>{scopeLabel}</dd>
          </dl>
        </div>

        {/* Действие: Экспорт PDF */}
        <Button variant="outline" size="sm" onClick={handleExportPdf} disabled={pdfPending}>
          <DownloadIcon className="size-4" />
          {pdfPending
            ? t("investigations.report.exportPdfPending")
            : t("investigations.report.exportPdf")}
        </Button>
      </div>

      {/* Резюме */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("investigations.report.summaryTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          {items.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              {t("investigations.report.summaryEmpty")}
            </p>
          ) : (
            <div className="space-y-3">
              {items.map((item, i) => (
                <div key={i} className="space-y-1">
                  <div className="flex items-center gap-2">
                    <StatusBadge variant={REPORT_SEVERITY_VARIANT[item.severity]}>
                      {t(`investigations.severity.${item.severity}`)}
                    </StatusBadge>
                    <span className="text-sm font-medium">{item.headline}</span>
                  </div>
                  <p className="text-muted-foreground text-sm">{item.recommendation}</p>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Что собрано */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("investigations.report.collectedTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          {collectionConfig ? (
            <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
              <dt className="text-muted-foreground">
                {t("investigations.report.collectedEvents")}
              </dt>
              <dd className="font-mono text-xs">{collectionConfig.events}</dd>

              <dt className="text-muted-foreground">
                {t("investigations.report.collectedFormat")}
              </dt>
              <dd>{collectionConfig.format}</dd>

              <dt className="text-muted-foreground">
                {t("investigations.report.collectedHistory")}
              </dt>
              <dd>
                {t("investigations.report.collectedHistoryHours", {
                  hours: collectionConfig.historyHours,
                })}
              </dd>

              {collectionConfig.durationThresholdMicros != null && (
                <>
                  <dt className="text-muted-foreground">
                    {t("investigations.report.collectedThreshold")}
                  </dt>
                  <dd>
                    {t("investigations.report.collectedThresholdMicros", {
                      micros: collectionConfig.durationThresholdMicros,
                    })}
                  </dd>
                </>
              )}

              {collectionConfig.processNameFilter && (
                <>
                  <dt className="text-muted-foreground">
                    {t("investigations.report.collectedProcessFilter")}
                  </dt>
                  <dd className="font-mono text-xs">{collectionConfig.processNameFilter}</dd>
                </>
              )}
            </dl>
          ) : null}

          <p
            className={`text-muted-foreground mt-3 text-xs ${collectionConfig ? "border-t pt-3" : ""}`}
          >
            {collectionConfig
              ? t("investigations.report.collectedRetention")
              : t("investigations.report.collectedNoConfig")}
          </p>
        </CardContent>
      </Card>

      {/* Находки */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("investigations.report.findingsTitle")}</CardTitle>
        </CardHeader>
        <CardContent>
          {items.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              {t("investigations.report.findingsEmpty")}
            </p>
          ) : (
            <div className="space-y-4">
              {items.map((item, i) => (
                <div key={i} className="space-y-2">
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <StatusBadge variant={REPORT_SEVERITY_VARIANT[item.severity]}>
                        {t(`investigations.severity.${item.severity}`)}
                      </StatusBadge>
                      <span className="text-sm font-medium">{item.headline}</span>
                    </div>
                    <span className="text-muted-foreground shrink-0 text-sm tabular-nums">
                      {item.count}
                    </span>
                  </div>
                  <p className="text-muted-foreground pl-2 text-sm">{item.recommendation}</p>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Подвал */}
      <p className="text-muted-foreground border-t pt-4 text-xs">
        {t("investigations.report.footer")}
      </p>
    </div>
  );
}
