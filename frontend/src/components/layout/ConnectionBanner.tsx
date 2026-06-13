import { useTranslation } from "react-i18next";
import { useIsOffline } from "@/lib/connectionStatus";

// MLC-121 (UX-03) — глобальный баннер состояния соединения. Виден, только пока
// последний сигнал — сетевой сбой (ApiNetworkError); снимается при следующем
// успешном запросе (markOnline). Текст — живой `errors.network` (раньше мёртвый).
export function ConnectionBanner() {
  const { t } = useTranslation();
  const offline = useIsOffline();

  if (!offline) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className="bg-status-danger/10 text-status-danger border-status-danger/30 border-b px-6 py-2 text-center text-sm font-medium"
    >
      {t("errors.network")}
    </div>
  );
}
