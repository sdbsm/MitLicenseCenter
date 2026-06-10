import {
  ArrowRightLeftIcon,
  CircleHelpIcon,
  DatabaseBackupIcon,
  GlobeIcon,
  LayersIcon,
  Loader2Icon,
  MoreHorizontalIcon,
  PencilIcon,
  RefreshCwIcon,
  TerminalIcon,
  Trash2Icon,
  WrenchIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import {
  toPublicationListItem,
  type PublicationListItem,
  type PublicationPublishStatus,
  type PublicationSource,
} from "@/features/publications/types";
import { statusBadgeClass } from "./infobaseFormat";
import type { InfobaseListItem } from "./types";

const PUBLISH_STATUS_VARIANT: Record<PublicationPublishStatus, StatusBadgeVariant> = {
  Published: "success",
  NotPublished: "info",
  Error: "danger",
  Unknown: "neutral",
};

// Источник публикации — иконка с tooltip вместо отдельной колонки (MLC-081):
// webinst = создана панелью, Конфигуратор = ручная (гейт перезаписи при публикации).
const SOURCE_ICON: Record<PublicationSource, typeof TerminalIcon> = {
  Webinst: TerminalIcon,
  Configurator: WrenchIcon,
  Unknown: CircleHelpIcon,
};

/** Выделение строк для bulk-операций: состояние «шапочного» чекбокса (MLC-081). */
export interface InfobaseHeaderSelection {
  checked: boolean | "indeterminate";
  onToggleAll: (checked: boolean) => void;
  disabled?: boolean;
}

/**
 * Заголовок таблицы инфобаз. Колонка «Клиент» опциональна (скрыта в карточке клиента);
 * колонка чекбоксов появляется, когда передано `selection` (страница «Базы»).
 */
export function InfobaseTableHeader({
  showTenant = false,
  selection,
}: {
  showTenant?: boolean;
  selection?: InfobaseHeaderSelection;
}) {
  const { t } = useTranslation();
  return (
    <TableHeader>
      <TableRow>
        {selection && (
          <TableHead className="w-10">
            <Checkbox
              checked={selection.checked}
              onCheckedChange={(v) => selection.onToggleAll(v === true)}
              disabled={selection.disabled}
              aria-label={t("publications.bulk.selectAll")}
            />
          </TableHead>
        )}
        <TableHead>{t("infobases.fields.name")}</TableHead>
        {showTenant && <TableHead>{t("infobases.fields.tenant")}</TableHead>}
        <TableHead>{t("infobases.fields.status")}</TableHead>
        <TableHead>{t("infobases.fields.publication")}</TableHead>
        <TableHead className="w-32">{t("infobases.fields.platformVersion")}</TableHead>
        <TableHead className="w-36">{t("infobases.fields.lastChecked")}</TableHead>
        <TableHead className="w-20" />
      </TableRow>
    </TableHeader>
  );
}

interface InfobaseRowProps {
  item: InfobaseListItem;
  /** Имя клиента; когда передано — рендерится колонка «Клиент». */
  tenantName?: string;
  isAdmin: boolean;
  onEdit: (item: InfobaseListItem) => void;
  onDelete: (item: InfobaseListItem) => void;
  /** Когда передано — в меню появляется пункт «Перенести в другого клиента». */
  onReassign?: (item: InfobaseListItem) => void;
  /** Открывает диалог бэкапов базы (MLC-078). Кнопка видна обеим ролям — запуск
   *  бэкапа доступен Viewer (ADR-27), поэтому она НЕ в admin-дропдауне. */
  onBackups: (item: InfobaseListItem) => void;
  /** MLC-081: операции с публикацией (страница «Базы»); опциональны —
   *  карточка клиента их не передаёт, и пункты меню не рендерятся. */
  onCheck?: (publication: PublicationListItem) => void;
  onPublish?: (publication: PublicationListItem) => void;
  onChangePlatform?: (publication: PublicationListItem) => void;
  /** Идёт проверка публикации этой строки («Проверить публикацию» задизейблен). */
  isChecking?: boolean;
  /** Выделение для bulk-операций; чекбокс рендерится, когда передан обработчик. */
  selected?: boolean;
  onToggleSelect?: (item: InfobaseListItem, checked: boolean) => void;
  /** MLC-096 — обратный дрейф: UUID базы отсутствует в снапшоте кластера 1С. Рендерит
   *  красную метку «Не найдена в кластере» рядом со статусом. Пусто для Viewer и при
   *  недоступном RAS (родитель отдаёт false). */
  missing?: boolean;
  /** Время опроса RAS — для тултипа метки «Не найдена в кластере». */
  missingCheckedAtUtc?: string;
}

/** Строка таблицы инфобаз (база + её публикация), общая для списка баз и карточки клиента. */
export function InfobaseRow({
  item,
  tenantName,
  isAdmin,
  onEdit,
  onDelete,
  onReassign,
  onBackups,
  onCheck,
  onPublish,
  onChangePlatform,
  isChecking = false,
  selected = false,
  onToggleSelect,
  missing = false,
  missingCheckedAtUtc,
}: InfobaseRowProps) {
  const { t } = useTranslation();
  const pub = item.publication;
  const SourceIcon = SOURCE_ICON[pub.source];
  const publicationTooltip = `${pub.siteName}${pub.virtualPath}${
    pub.lastCheckDetails ? `\n${pub.lastCheckDetails}` : ""
  }`;

  return (
    <TableRow data-state={selected ? "selected" : undefined}>
      <TooltipProvider delayDuration={150}>
        {onToggleSelect && (
          <TableCell className="w-10">
            <Checkbox
              checked={selected}
              onCheckedChange={(v) => onToggleSelect(item, v === true)}
              aria-label={t("publications.bulk.selectRow")}
            />
          </TableCell>
        )}
        <TableCell className="font-medium">{item.name}</TableCell>
        {tenantName !== undefined && (
          <TableCell className="text-muted-foreground">{tenantName}</TableCell>
        )}
        <TableCell>
          <div className="flex flex-wrap items-center gap-1.5">
            <Badge className={statusBadgeClass(item.status)}>
              {t(`infobases.status.${item.status}`)}
            </Badge>
            {missing && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="cursor-help">
                    <StatusBadge variant="danger">{t("infobases.missing.label")}</StatusBadge>
                  </span>
                </TooltipTrigger>
                <TooltipContent className="max-w-sm">
                  {t("infobases.missing.tooltip")}
                  {missingCheckedAtUtc && (
                    <>
                      {" ("}
                      <RelativeTime value={missingCheckedAtUtc} />
                      {")"}
                    </>
                  )}
                </TooltipContent>
              </Tooltip>
            )}
          </div>
        </TableCell>
        <TableCell>
          <div className="flex items-center gap-1.5">
            <Tooltip>
              <TooltipTrigger asChild>
                <span className="cursor-help">
                  <StatusBadge variant={PUBLISH_STATUS_VARIANT[pub.lastCheckStatus]}>
                    {t(`publications.status.${pub.lastCheckStatus.toLowerCase()}`)}
                  </StatusBadge>
                </span>
              </TooltipTrigger>
              <TooltipContent className="max-w-sm whitespace-pre-line">
                {publicationTooltip}
              </TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger asChild>
                <span className="text-muted-foreground inline-flex cursor-help">
                  <SourceIcon className="size-3.5" />
                </span>
              </TooltipTrigger>
              <TooltipContent className="max-w-sm">
                {t(`infobases.source.${pub.source.toLowerCase()}`)}
              </TooltipContent>
            </Tooltip>
            {isChecking && <Loader2Icon className="text-muted-foreground size-3.5 animate-spin" />}
          </div>
        </TableCell>
        <TableCell className="text-muted-foreground font-mono text-xs">
          {pub.platformVersion}
        </TableCell>
        <TableCell className="text-muted-foreground">
          {pub.lastCheckAt ? (
            <RelativeTime value={pub.lastCheckAt} />
          ) : (
            <span>{t("publications.status.unknown")}</span>
          )}
        </TableCell>
        <TableCell className="text-right">
          <div className="flex items-center justify-end gap-0.5">
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="size-8"
                  onClick={() => onBackups(item)}
                >
                  <DatabaseBackupIcon className="size-4" />
                  <span className="sr-only">{t("backups.rowAction")}</span>
                </Button>
              </TooltipTrigger>
              <TooltipContent>{t("backups.rowAction")}</TooltipContent>
            </Tooltip>
            {isAdmin && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="icon" className="size-8">
                    <MoreHorizontalIcon className="size-4" />
                    <span className="sr-only">{t("common.details")}</span>
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onSelect={() => onEdit(item)}>
                    <PencilIcon className="size-4" />
                    {t("common.edit")}
                  </DropdownMenuItem>
                  {onCheck && (
                    <DropdownMenuItem
                      disabled={isChecking}
                      onSelect={() => onCheck(toPublicationListItem(item))}
                    >
                      <RefreshCwIcon className="size-4" />
                      {t("publications.actions.check")}
                    </DropdownMenuItem>
                  )}
                  {onPublish && (
                    <DropdownMenuItem onSelect={() => onPublish(toPublicationListItem(item))}>
                      <GlobeIcon className="size-4" />
                      {t("publications.actions.publish")}
                    </DropdownMenuItem>
                  )}
                  {onChangePlatform && (
                    <DropdownMenuItem
                      onSelect={() => onChangePlatform(toPublicationListItem(item))}
                    >
                      <LayersIcon className="size-4" />
                      {t("publications.actions.changePlatform")}
                    </DropdownMenuItem>
                  )}
                  {onReassign && (
                    <DropdownMenuItem onSelect={() => onReassign(item)}>
                      <ArrowRightLeftIcon className="size-4" />
                      {t("infobases.reassign.action")}
                    </DropdownMenuItem>
                  )}
                  <DropdownMenuItem variant="destructive" onSelect={() => onDelete(item)}>
                    <Trash2Icon className="size-4" />
                    {t("common.delete")}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        </TableCell>
      </TooltipProvider>
    </TableRow>
  );
}
