import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useTranslation } from "react-i18next";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { LicenseUsageChart } from "./LicenseUsageChart";
import { ReportsEmptyState } from "./ReportsEmptyState";
import { ReportsStats } from "./ReportsStats";
import type { LicenseUsageSeriesResponse } from "./types";

interface LicenseUsageSummaryProps {
  data: LicenseUsageSeriesResponse | undefined;
  isLoading: boolean;
}

/** Секция сводки по всем клиентам: эффективный период, стат-тексты, график и
 *  обязательная оговорка про обзорность суммы по бакету (решение MLC-049). */
export function LicenseUsageSummary({ data, isLoading }: LicenseUsageSummaryProps) {
  const { t } = useTranslation();
  const isEmpty = !data || data.buckets.length === 0;

  return (
    <Card>
      <CardHeader className="gap-2">
        <CardTitle>{t("reports.summary.title")}</CardTitle>
        {data && (
          <p className="text-muted-foreground text-sm">
            {t("reports.summary.effectiveRange", {
              from: format(new Date(data.fromUtc), "dd.MM.yyyy HH:mm", { locale: ru }),
              to: format(new Date(data.toUtc), "dd.MM.yyyy HH:mm", { locale: ru }),
            })}
          </p>
        )}
      </CardHeader>
      <CardContent className="space-y-3">
        {data && !isEmpty && <ReportsStats data={data} />}

        {isLoading && !data ? (
          <Skeleton className="h-[320px] w-full" />
        ) : isEmpty ? (
          <ReportsEmptyState />
        ) : (
          <LicenseUsageChart buckets={data.buckets} isLoading={isLoading} />
        )}

        {/* Оговорка: сумма по бакету — обзорная цифра, не одновременный пик платформы. */}
        {!isEmpty && (
          <p className="text-muted-foreground border-l-2 pl-3 text-xs">
            {t("reports.summary.caveat")}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
