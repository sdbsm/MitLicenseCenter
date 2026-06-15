import { useTranslation } from "react-i18next";
import { useMe } from "@/features/auth/useAuth";
import { useUpdateStatus } from "./useUpdates";

// MLC-176 (ADR-50) — глобальный баннер «Доступна версия X.Y.Z». Виден ВСЕМ ролям;
// ссылка «Открыть релиз» — всем; кнопка «Скачать установщик» — только Admin и только
// при наличии downloadUrl (ассет .exe в релизе). Запуск установщика — руками админа
// под UAC; здесь только ссылка на файл. Рендерится в AppShell рядом с ConnectionBanner.
export function UpdateBanner() {
  const { t } = useTranslation();
  const { data } = useUpdateStatus();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  // При ошибке/недоступной проверке data?.updateAvailable=false → баннер скрыт.
  if (!data?.updateAvailable) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className="bg-status-info/10 text-status-info border-status-info/30 flex flex-wrap items-center justify-center gap-x-4 gap-y-1 border-b px-6 py-2 text-center text-sm font-medium"
    >
      <span>{t("updates.banner.available", { version: data.latestVersion })}</span>
      {data.releaseUrl && (
        <a
          href={data.releaseUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="underline underline-offset-2"
        >
          {t("updates.banner.openRelease")}
        </a>
      )}
      {isAdmin && data.downloadUrl && (
        <a href={data.downloadUrl} download className="underline underline-offset-2">
          {t("updates.banner.download")}
        </a>
      )}
    </div>
  );
}
