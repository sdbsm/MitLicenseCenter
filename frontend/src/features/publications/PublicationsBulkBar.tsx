import { CircleSlashIcon, MoreHorizontalIcon, RefreshCwIcon, Trash2Icon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

interface PublicationsBulkBarProps {
  count: number;
  onPublish: () => void;
  onChangePlatform: () => void;
  onCheck: () => void;
  onUnpublish: () => void;
  onDeleteInfobase: () => void;
  onClear: () => void;
  // MLC-181c — «Выбрать все N по фильтру»: дёргает /infobases/ids и наполняет тот же внешний
  // выбор всеми пригодными для bulk строками по текущему фильтру (за пределами страницы).
  onSelectAllFiltered: () => void;
  // Идёт запрос /ids (кнопка disabled, пока грузим набор по фильтру).
  isSelectingAllFiltered?: boolean;
}

// MLC-046 / MLC-184b: панель массовых действий. Видна, когда выбрана хотя бы одна публикация
// (рендерится вызывающим только для admin). Две зоны (MLC-184b): слева — выбор («Выбрано: N» +
// «Выбрать все по фильтру» + «Снять»), справа — действия (Опубликовать / Сменить платформу /
// «Ещё ▾» с проверкой, снятием с публикации и удалением базы).
export function PublicationsBulkBar({
  count,
  onPublish,
  onChangePlatform,
  onCheck,
  onUnpublish,
  onDeleteInfobase,
  onClear,
  onSelectAllFiltered,
  isSelectingAllFiltered,
}: PublicationsBulkBarProps) {
  const { t } = useTranslation();

  return (
    <div className="bg-muted/40 flex flex-wrap items-center justify-between gap-3 rounded-md border px-4 py-2">
      {/* Зона ВЫБОРА */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{t("publications.bulk.selected", { count })}</span>
        {/* MLC-181c — выбор всего отфильтрованного набора (через 2+ страницы), а не только
            видимой страницы. */}
        <Button
          variant="link"
          size="sm"
          onClick={onSelectAllFiltered}
          disabled={isSelectingAllFiltered}
        >
          {t("publications.bulk.selectAllFiltered")}
        </Button>
        <Button variant="ghost" size="sm" onClick={onClear}>
          {t("publications.bulk.clear")}
        </Button>
      </div>

      {/* Зона ДЕЙСТВИЙ */}
      <div className="flex flex-wrap items-center gap-2">
        <Button size="sm" onClick={onPublish}>
          {t("publications.bulk.publishAction")}
        </Button>
        <Button size="sm" variant="outline" onClick={onChangePlatform}>
          {t("publications.bulk.changePlatformAction")}
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button size="sm" variant="outline">
              <MoreHorizontalIcon className="size-4" />
              {t("publications.bulk.moreActions")}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onSelect={onCheck}>
              <RefreshCwIcon className="size-4" />
              {t("publications.bulk.recheckAction")}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onSelect={onUnpublish}>
              <CircleSlashIcon className="size-4" />
              {t("publications.bulk.unpublishAction")}
            </DropdownMenuItem>
            <DropdownMenuItem variant="destructive" onSelect={onDeleteInfobase}>
              <Trash2Icon className="size-4" />
              {t("publications.bulk.deleteInfobaseAction")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  );
}
