import {
  ArrowRightLeftIcon,
  DatabaseBackupIcon,
  MoreHorizontalIcon,
  PencilIcon,
  Trash2Icon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { formatInfobaseDateTime, statusBadgeClass } from "./infobaseFormat";
import type { InfobaseListItem } from "./types";

/** Заголовок таблицы инфобаз. Колонка «Клиент» опциональна (скрыта в карточке клиента и в группах). */
export function InfobaseTableHeader({ showTenant = false }: { showTenant?: boolean }) {
  const { t } = useTranslation();
  return (
    <TableHeader>
      <TableRow>
        <TableHead>{t("infobases.fields.name")}</TableHead>
        {showTenant && <TableHead>{t("infobases.fields.tenant")}</TableHead>}
        <TableHead>{t("infobases.fields.databaseServer")}</TableHead>
        <TableHead>{t("infobases.fields.databaseName")}</TableHead>
        <TableHead>{t("infobases.fields.status")}</TableHead>
        <TableHead>{t("infobases.fields.publication")}</TableHead>
        <TableHead>{t("infobases.fields.updatedAt")}</TableHead>
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
}

/** Строка таблицы инфобазы, общая для списка баз и карточки клиента. */
export function InfobaseRow({
  item,
  tenantName,
  isAdmin,
  onEdit,
  onDelete,
  onReassign,
  onBackups,
}: InfobaseRowProps) {
  const { t } = useTranslation();
  return (
    <TableRow>
      <TableCell className="font-medium">{item.name}</TableCell>
      {tenantName !== undefined && (
        <TableCell className="text-muted-foreground">{tenantName}</TableCell>
      )}
      <TableCell className="text-muted-foreground tabular-nums">{item.databaseServer}</TableCell>
      <TableCell className="text-muted-foreground tabular-nums">{item.databaseName}</TableCell>
      <TableCell>
        <Badge className={statusBadgeClass(item.status)}>
          {t(`infobases.status.${item.status}`)}
        </Badge>
      </TableCell>
      <TableCell className="text-muted-foreground tabular-nums">
        <span className="font-mono text-xs">{item.publication.virtualPath}</span>
        <span className="text-muted-foreground/70 ml-2 text-xs">
          {item.publication.platformVersion}
        </span>
      </TableCell>
      <TableCell className="text-muted-foreground tabular-nums">
        {formatInfobaseDateTime(item.updatedAt ?? item.createdAt)}
      </TableCell>
      <TableCell className="text-right">
        <div className="flex items-center justify-end gap-0.5">
          <TooltipProvider delayDuration={150}>
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
          </TooltipProvider>
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
    </TableRow>
  );
}
