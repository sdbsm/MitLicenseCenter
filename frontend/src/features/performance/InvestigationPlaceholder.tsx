import { useTranslation } from "react-i18next";
import { FlaskConical } from "lucide-react";
import { Button } from "@/components/ui/button";

/**
 * Режим «Расследование» — заглушка-точка входа (MLC-241, ADR-57).
 *
 * Полноценные экраны Мастер/Дело/Отчёт/Список/Прогресс — последующие задачи
 * MLC-242..244. Здесь: объяснение режима + CTA «Новое расследование» как
 * заметная точка входа (пока placeholder — экрана нет).
 *
 * Сетевых запросов не делает; хуки features/investigations намеренно не подключены
 * (это MLC-243).
 */
export function InvestigationPlaceholder() {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col items-center justify-center gap-6 py-20 text-center">
      <div className="bg-muted flex size-16 items-center justify-center rounded-full">
        <FlaskConical className="text-muted-foreground size-8" aria-hidden="true" />
      </div>

      <div className="max-w-md space-y-2">
        <h3 className="text-lg font-semibold">{t("performance.investigationPlaceholder.title")}</h3>
        <p className="text-muted-foreground text-sm">
          {t("performance.investigationPlaceholder.description")}
        </p>
      </div>

      <Button disabled title={t("performance.investigationPlaceholder.comingSoon")}>
        {t("performance.investigationPlaceholder.cta")}
      </Button>
      <p className="text-muted-foreground text-xs">
        {t("performance.investigationPlaceholder.comingSoon")}
      </p>
    </div>
  );
}
