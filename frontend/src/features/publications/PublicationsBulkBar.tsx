import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";

interface PublicationsBulkBarProps {
  count: number;
  onPublish: () => void;
  onChangePlatform: () => void;
  onClear: () => void;
}

// MLC-046: панель массовых действий. Видна, когда выбрана хотя бы одна публикация
// (рендерится вызывающим только для admin).
export function PublicationsBulkBar({
  count,
  onPublish,
  onChangePlatform,
  onClear,
}: PublicationsBulkBarProps) {
  const { t } = useTranslation();

  return (
    <div className="bg-muted/40 flex flex-wrap items-center gap-3 rounded-md border px-4 py-2">
      <span className="text-sm font-medium">{t("publications.bulk.selected", { count })}</span>
      <div className="flex flex-wrap gap-2">
        <Button size="sm" onClick={onPublish}>
          {t("publications.bulk.publishAction")}
        </Button>
        <Button size="sm" variant="outline" onClick={onChangePlatform}>
          {t("publications.bulk.changePlatformAction")}
        </Button>
        <Button size="sm" variant="ghost" onClick={onClear}>
          {t("publications.bulk.clear")}
        </Button>
      </div>
    </div>
  );
}
