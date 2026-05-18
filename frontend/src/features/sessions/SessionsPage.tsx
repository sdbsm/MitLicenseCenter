import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { MonitorPlayIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useSessionsSnapshot } from "./useSessionsSnapshot";

export function SessionsPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useSessionsSnapshot();

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("sessions.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("sessions.subtitle")}</p>
      </div>

      <Card className="max-w-xl">
        <CardHeader>
          <div className="flex items-start gap-3">
            <MonitorPlayIcon className="text-muted-foreground mt-1 size-5" />
            <div className="space-y-1">
              <CardTitle>{t("sessions.stub.title")}</CardTitle>
              <CardDescription>{t("sessions.stub.body")}</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {isLoading ? (
            <Skeleton className="h-4 w-40" />
          ) : isError ? (
            <p className="text-destructive">{t("sessions.errors.loadFailed")}</p>
          ) : (
            <>
              <p>
                <span className="text-muted-foreground">
                  {t("sessions.fields.active")}:{" "}
                </span>
                <span className="font-medium tabular-nums">{data?.items.length ?? 0}</span>
              </p>
              {data?.capturedAt && (
                <p className="text-muted-foreground text-xs tabular-nums">
                  {t("sessions.fields.capturedAt")}:{" "}
                  {format(new Date(data.capturedAt), "dd.MM.yyyy HH:mm:ss", { locale: ru })}
                </p>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
