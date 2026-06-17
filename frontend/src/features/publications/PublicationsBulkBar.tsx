import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";

interface PublicationsBulkBarProps {
  count: number;
  onPublish: () => void;
  onChangePlatform: () => void;
  onClear: () => void;
  // MLC-181c — «Выбрать все N по фильтру»: дёргает /infobases/ids и наполняет тот же внешний
  // выбор всеми пригодными для bulk строками по текущему фильтру (за пределами страницы).
  onSelectAllFiltered: () => void;
  // Идёт запрос /ids (кнопка disabled, пока грузим набор по фильтру).
  isSelectingAllFiltered?: boolean;
}

// MLC-046: панель массовых действий. Видна, когда выбрана хотя бы одна публикация
// (рендерится вызывающим только для admin).
export function PublicationsBulkBar({
  count,
  onPublish,
  onChangePlatform,
  onClear,
  onSelectAllFiltered,
  isSelectingAllFiltered,
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
        {/* MLC-181c — выбор всего отфильтрованного набора (через 2+ страницы), а не только
            видимой страницы. */}
        <Button
          size="sm"
          variant="outline"
          onClick={onSelectAllFiltered}
          disabled={isSelectingAllFiltered}
        >
          {t("publications.bulk.selectAllFiltered")}
        </Button>
        <Button size="sm" variant="ghost" onClick={onClear}>
          {t("publications.bulk.clear")}
        </Button>
      </div>
    </div>
  );
}
