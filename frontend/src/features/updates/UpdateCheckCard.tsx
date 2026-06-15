import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useCheckNow, useUpdateStatus } from "./useUpdates";

// MLC-176 (ADR-50) — карточка «Обновления» в разделе «Параметры» (Admin-only:
// страница и так Admin). Показывает текущую и последнюю версии + кнопку «Проверить
// сейчас» (форс-проверка через GitHub). Установщик запускается вручную под UAC —
// здесь только ссылка на релиз и файл .exe.
export function UpdateCheckCard() {
  const { t } = useTranslation();
  const { data } = useUpdateStatus();
  const checkNow = useCheckNow();

  const status = checkNow.data ?? data;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("updates.card.title")}</CardTitle>
        <CardDescription>{t("updates.card.description")}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 text-sm">
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
          <dt className="text-muted-foreground">{t("updates.card.currentVersion")}</dt>
          <dd className="font-medium">{status?.currentVersion ?? "—"}</dd>
          {status?.checkAvailable && (
            <>
              <dt className="text-muted-foreground">{t("updates.card.latestVersion")}</dt>
              <dd className="font-medium">{status.latestVersion ?? "—"}</dd>
            </>
          )}
        </dl>

        {status &&
          (status.checkAvailable ? (
            status.updateAvailable ? (
              <p className="text-status-info">
                {t("updates.card.updateAvailable", { version: status.latestVersion })}
              </p>
            ) : (
              <p className="text-muted-foreground">{t("updates.card.upToDate")}</p>
            )
          ) : (
            <p className="text-muted-foreground">{t("updates.card.unavailable")}</p>
          ))}

        <div className="flex flex-wrap items-center gap-3">
          <Button
            size="sm"
            variant="outline"
            onClick={() => checkNow.mutate()}
            disabled={checkNow.isPending}
          >
            {checkNow.isPending ? t("updates.card.checking") : t("updates.card.checkNow")}
          </Button>
          {status?.updateAvailable && status.releaseUrl && (
            <a
              href={status.releaseUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm underline underline-offset-2"
            >
              {t("updates.card.openRelease")}
            </a>
          )}
          {status?.updateAvailable && status.downloadUrl && (
            <a href={status.downloadUrl} download className="text-sm underline underline-offset-2">
              {t("updates.card.download")}
            </a>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
