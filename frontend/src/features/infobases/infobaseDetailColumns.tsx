import {
  ArrowRightLeftIcon,
  CircleHelpIcon,
  DatabaseBackupIcon,
  MoreHorizontalIcon,
  PencilIcon,
  Trash2Icon,
  TerminalIcon,
  WrenchIcon,
} from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import type { PublicationPublishStatus, PublicationSource } from "@/features/publications/types";
import { SizeCell } from "./infobaseColumns";
import { statusBadgeClass } from "./infobaseFormat";
import type { InfobaseListItem } from "./types";

const PUBLISH_STATUS_VARIANT: Record<PublicationPublishStatus, StatusBadgeVariant> = {
  Published: "success",
  NotPublished: "info",
  Error: "danger",
  Unknown: "neutral",
};

const SOURCE_ICON: Record<PublicationSource, typeof TerminalIcon> = {
  Webinst: TerminalIcon,
  Configurator: WrenchIcon,
  Unknown: CircleHelpIcon,
};

export interface InfobaseDetailColumnContext {
  t: TFunction;
  isAdmin: boolean;
  /** Список клиентов > 1: показывать «Перенести». */
  canReassign: boolean;
  onEdit: (item: InfobaseListItem) => void;
  onDelete: (item: InfobaseListItem) => void;
  onReassign?: (item: InfobaseListItem) => void;
  onBackups: (item: InfobaseListItem) => void;
}

/**
 * Колонки таблицы инфобаз для карточки клиента (`DataTable`, MLC-144c).
 * Серверная пагинация — сортировка отсутствует (`enableSorting: false`).
 * Колонка «Клиент» не нужна (всегда один tenantId из URL-params).
 * Статусы рендерятся только через `StatusBadge` (инвариант проекта).
 */
export function buildInfobaseDetailColumns(
  ctx: InfobaseDetailColumnContext
): ColumnDef<InfobaseListItem>[] {
  const { t, isAdmin, canReassign, onEdit, onDelete, onReassign, onBackups } = ctx;

  return [
    {
      id: "name",
      accessorKey: "name",
      header: t("infobases.fields.name"),
      enableSorting: false,
      meta: { label: t("infobases.fields.name") },
      cell: ({ row }) => <span className="font-medium">{row.original.name}</span>,
    },
    {
      id: "status",
      accessorKey: "status",
      header: t("infobases.fields.status"),
      enableSorting: false,
      meta: { label: t("infobases.fields.status") },
      cell: ({ row }) => (
        <Badge className={statusBadgeClass(row.original.status)}>
          {t(`infobases.status.${row.original.status}`)}
        </Badge>
      ),
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
          <TooltipProvider delayDuration={150}>
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
            </div>
          </TooltipProvider>
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
      id: "size",
      header: t("infobases.fields.size"),
      enableSorting: false,
      meta: {
        label: t("infobases.fields.size"),
        headClassName: "w-24",
        cellClassName: "text-muted-foreground",
      },
      cell: ({ row }) => (
        <TooltipProvider delayDuration={150}>
          <SizeCell item={row.original} t={t} />
        </TooltipProvider>
      ),
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
        const lastCheckAt = row.original.publication.lastCheckAt;
        return lastCheckAt ? (
          <RelativeTime value={lastCheckAt} />
        ) : (
          <span>{t("publications.status.unknown")}</span>
        );
      },
    },
    {
      id: "actions",
      header: "",
      enableHiding: false,
      enableSorting: false,
      meta: { headClassName: "w-20", cellClassName: "text-right" },
      cell: ({ row }) => {
        const item = row.original;
        return (
          <TooltipProvider delayDuration={150}>
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
                    {canReassign && onReassign && (
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
          </TooltipProvider>
        );
      },
    },
  ];
}
