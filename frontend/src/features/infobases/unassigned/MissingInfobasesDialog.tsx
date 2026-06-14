import { useState } from "react";
import { AlertTriangleIcon, Trash2Icon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { PaginationBar } from "@/components/PaginationBar";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type { MissingInfobase } from "./types";

interface MissingInfobasesDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: MissingInfobase[];
  onDelete: (item: MissingInfobase) => void;
}

const UUID_CLASS = "font-mono text-xs text-muted-foreground";

const MISSING_PAGE_SIZE = 20;

/**
 * MLC-096 — диалог обратного дрейфа: записи панели, чьего UUID нет в кластере 1С. Обычный
 * `Dialog` (06 §7): сам по себе действий не совершает, «Удалить» открывает существующий
 * диалог удаления инфобазы (AlertDialog-подтверждение по имени). Строка = клиент · имя ·
 * UUID-моно (06 §4). Пустого состояния нет — диалог открывается только при count > 0.
 * Пагинация клиентская (UX-15): список отсортирован по tenantName + name, страница
 * clamp'ится при изменении items (live-снапшот).
 */
export function MissingInfobasesDialog({
  open,
  onOpenChange,
  items,
  onDelete,
}: MissingInfobasesDialogProps) {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);

  // Стабильная сортировка: клиент → имя базы
  const sorted = [...items].sort((a, b) => {
    const byCli = a.tenantName.localeCompare(b.tenantName, "ru");
    return byCli !== 0 ? byCli : a.name.localeCompare(b.name, "ru");
  });

  const totalPages = Math.max(1, Math.ceil(sorted.length / MISSING_PAGE_SIZE));
  // Clamp при изменении длины items (живой снапшот)
  const safePage = Math.min(page, totalPages);
  const start = (safePage - 1) * MISSING_PAGE_SIZE;
  const pageItems = sorted.slice(start, start + MISSING_PAGE_SIZE);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t("infobases.missing.dialog.title")}</DialogTitle>
          <DialogDescription>{t("infobases.missing.dialog.subtitle")}</DialogDescription>
        </DialogHeader>

        <ul className="divide-y rounded-md border" aria-label={t("infobases.missing.dialog.title")}>
          {pageItems.map((item) => (
            <li key={item.infobaseId} className="flex flex-wrap items-center gap-x-3 gap-y-1 p-3">
              <AlertTriangleIcon className="size-4 shrink-0 text-rose-600 dark:text-rose-400" />
              <div className="min-w-0 flex-1">
                <p className="truncate font-medium" title={item.name}>
                  {item.name}
                </p>
                <p className="text-muted-foreground truncate text-sm" title={item.tenantName}>
                  {item.tenantName}
                </p>
                <p className={UUID_CLASS}>{item.clusterInfobaseId}</p>
              </div>
              <Button variant="ghost" size="sm" onClick={() => onDelete(item)}>
                <Trash2Icon className="size-4" />
                {t("infobases.missing.dialog.delete")}
              </Button>
            </li>
          ))}
        </ul>

        <PaginationBar
          page={safePage}
          pageSize={MISSING_PAGE_SIZE}
          total={sorted.length}
          onPageChange={setPage}
        />
      </DialogContent>
    </Dialog>
  );
}
