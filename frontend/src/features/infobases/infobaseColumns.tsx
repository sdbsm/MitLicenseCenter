import {
  ArrowRightLeftIcon,
  CircleHelpIcon,
  CircleSlashIcon,
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
import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";
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
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
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

export interface InfobaseColumnContext {
  t: TFunction;
  isAdmin: boolean;
  /** Имя клиента по id (колонка «Клиент»). */
  tenantNameById: Map<string, string>;
  /** Идёт ли проверка публикации этой строки (id публикации). */
  checkingId: string | null;
  /** Membership-набор UUID кластера для метки «Не найдена в кластере» (MLC-096). */
  missingSet: Set<string>;
  /** Время опроса RAS — тултип метки «Не найдена в кластере». */
  missingCheckedAtUtc?: string;
  // Выделение для bulk-операций (MLC-081) — внешний Map переживает листание страниц.
  isSelected: (publicationId: string) => boolean;
  onToggleSelect: (item: InfobaseListItem, checked: boolean) => void;
  headerChecked: boolean | "indeterminate";
  onToggleAll: (checked: boolean) => void;
  selectionDisabled: boolean;
  onEdit: (item: InfobaseListItem) => void;
  onDelete: (item: InfobaseListItem) => void;
  onReassign?: (item: InfobaseListItem) => void;
  onBackups: (item: InfobaseListItem) => void;
  onCheck?: (publication: PublicationListItem) => void;
  onPublish?: (publication: PublicationListItem) => void;
  onUnpublish?: (publication: PublicationListItem) => void;
  onChangePlatform?: (publication: PublicationListItem) => void;
}

/**
 * Колонки таблицы баз для `DataTable` (MLC-144b). Серверная пагинация/фильтрация —
 * сортировка на сервере не применяется, поэтому колонки несортируемые
 * (`enableSorting:false`). Bulk-выбор питается внешним `Map<id, PublicationListItem>`
 * (переживает листание/смену фильтра), а не tanstack row-selection: колонка чекбоксов
 * рендерится только для админа. Статусы публикации и метка «не найдена» — только через
 * `StatusBadge` (инвариант). Ячейки 1:1 повторяют прежний `InfobaseRow`.
 */
export function buildInfobaseColumns(ctx: InfobaseColumnContext): ColumnDef<InfobaseListItem>[] {
  const {
    t,
    isAdmin,
    tenantNameById,
    checkingId,
    missingSet,
    missingCheckedAtUtc,
    isSelected,
    onToggleSelect,
    headerChecked,
    onToggleAll,
    selectionDisabled,
    onEdit,
    onDelete,
    onReassign,
    onBackups,
    onCheck,
    onPublish,
    onUnpublish,
    onChangePlatform,
  } = ctx;

  const columns: ColumnDef<InfobaseListItem>[] = [];

  // Колонка чекбоксов bulk-выбора — только для админа (Viewer видит read-only список).
  if (isAdmin) {
    columns.push({
      id: "select",
      header: () => (
        <Checkbox
          checked={headerChecked}
          onCheckedChange={(v) => onToggleAll(v === true)}
          disabled={selectionDisabled}
          aria-label={t("publications.bulk.selectAll")}
        />
      ),
      enableSorting: false,
      enableHiding: false,
      meta: { headClassName: "w-10" },
      cell: ({ row }) => (
        <Checkbox
          checked={isSelected(row.original.publication.id)}
          onCheckedChange={(v) => onToggleSelect(row.original, v === true)}
          aria-label={t("publications.bulk.selectRow")}
        />
      ),
    });
  }

  columns.push(
    {
      id: "name",
      accessorKey: "name",
      header: t("infobases.fields.name"),
      enableSorting: false,
      meta: { label: t("infobases.fields.name"), cellClassName: "font-medium" },
      cell: ({ row }) => row.original.name,
    },
    {
      id: "tenant",
      header: t("infobases.fields.tenant"),
      enableSorting: false,
      meta: { label: t("infobases.fields.tenant"), cellClassName: "text-muted-foreground" },
      cell: ({ row }) => tenantNameById.get(row.original.tenantId) ?? row.original.tenantName,
    },
    {
      id: "status",
      accessorKey: "status",
      header: t("infobases.fields.status"),
      enableSorting: false,
      meta: { label: t("infobases.fields.status") },
      cell: ({ row }) => {
        const item = row.original;
        const missing = missingSet.has(item.clusterInfobaseId);
        return (
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
        );
      },
    },
    {
      id: "publication",
      header: t("infobases.fields.publication"),
      enableSorting: false,
      meta: { label: t("infobases.fields.publication") },
      cell: ({ row }) => {
        const pub = row.original.publication;
        const SourceIcon = SOURCE_ICON[pub.source];
        const publicationTooltip = `${pub.siteName}${pub.virtualPath}${
          pub.lastCheckDetails ? `\n${pub.lastCheckDetails}` : ""
        }`;
        return (
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
            {checkingId === pub.id && (
              <Loader2Icon className="text-muted-foreground size-3.5 animate-spin" />
            )}
          </div>
        );
      },
    },
    {
      id: "platformVersion",
      header: t("infobases.fields.platformVersion"),
      enableSorting: false,
      meta: {
        label: t("infobases.fields.platformVersion"),
        headClassName: "w-32",
        cellClassName: "text-muted-foreground font-mono text-xs",
      },
      cell: ({ row }) => row.original.publication.platformVersion,
    },
    {
      id: "lastChecked",
      header: t("infobases.fields.lastChecked"),
      enableSorting: false,
      meta: {
        label: t("infobases.fields.lastChecked"),
        headClassName: "w-36",
        cellClassName: "text-muted-foreground",
      },
      cell: ({ row }) => {
        const pub = row.original.publication;
        return pub.lastCheckAt ? (
          <RelativeTime value={pub.lastCheckAt} />
        ) : (
          <span>{t("publications.status.unknown")}</span>
        );
      },
    },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      enableHiding: false,
      meta: { headClassName: "w-20", cellClassName: "text-right" },
      cell: ({ row }) => {
        const item = row.original;
        const isChecking = checkingId === item.publication.id;
        return (
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
                  {onUnpublish && (
                    <DropdownMenuItem
                      variant="destructive"
                      onSelect={() => onUnpublish(toPublicationListItem(item))}
                    >
                      <CircleSlashIcon className="size-4" />
                      {t("publications.actions.unpublish")}
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
        );
      },
    }
  );

  return columns;
}
