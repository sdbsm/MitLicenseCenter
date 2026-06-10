import { DatabaseIcon, EyeOffIcon, RotateCcwIcon, ServerOffIcon } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { matchConflictCode } from "@/lib/apiErrors";
import { useHideUnassignedInfobase, useUnhideUnassignedInfobase } from "./useUnassignedInfobases";
import type { HiddenUnassignedInfobase, UnassignedInfobaseItem } from "./types";

interface UnassignedInfobasesDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: UnassignedInfobaseItem[];
  hiddenItems: HiddenUnassignedInfobase[];
  available: boolean;
  checkedAtUtc: string | null;
  isLoading: boolean;
  isRefreshing: boolean;
  onRefresh: () => void;
  onAssign: (item: UnassignedInfobaseItem) => void;
  onManualEntry: () => void;
}

const UUID_CLASS = "font-mono text-xs text-muted-foreground";

/**
 * MLC-093 — диалог разбора нераспределённых баз кластера. Обычный `Dialog` (действия
 * обратимы, `AlertDialog` не нужен — 06 §7). Строка = имя + UUID (моноширинный, 06 §4);
 * действия «Назначить» (→ форма с префиллом) и «Скрыть» (в игнор-лист, без подтверждения).
 * Свёрнутый блок «Скрытые: N» с «Вернуть». Блок скрытых рендерится всегда — даже при
 * недоступном RAS (приходит из БД-снапшота). Внизу — «Ввести вручную» (fallback на пустую
 * форму) и свежесть опроса RAS.
 */
export function UnassignedInfobasesDialog({
  open,
  onOpenChange,
  items,
  hiddenItems,
  available,
  checkedAtUtc,
  isLoading,
  isRefreshing,
  onRefresh,
  onAssign,
  onManualEntry,
}: UnassignedInfobasesDialogProps) {
  const { t } = useTranslation();
  const hide = useHideUnassignedInfobase();
  const unhide = useUnhideUnassignedInfobase();
  const [showHidden, setShowHidden] = useState(false);

  const handleHide = async (item: UnassignedInfobaseItem) => {
    try {
      await hide.mutateAsync({ clusterInfobaseId: item.clusterInfobaseId, name: item.name });
    } catch (error) {
      const messageKey = matchConflictCode(error, {
        UNASSIGNED_ALREADY_ASSIGNED: "infobases.unassigned.errors.alreadyAssigned",
        UNASSIGNED_ALREADY_HIDDEN: "infobases.unassigned.errors.alreadyHidden",
      });
      toast.error(messageKey ? t(messageKey) : t("errors.generic"));
    }
  };

  const handleUnhide = async (hidden: HiddenUnassignedInfobase) => {
    try {
      await unhide.mutateAsync(hidden.clusterInfobaseId);
    } catch {
      toast.error(t("errors.generic"));
    }
  };

  const pending = hide.isPending || unhide.isPending;
  const showEmptyState = available && !isLoading && items.length === 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t("infobases.unassigned.dialog.title")}</DialogTitle>
          <DialogDescription>{t("infobases.unassigned.dialog.subtitle")}</DialogDescription>
        </DialogHeader>

        {!available && (
          <div className="text-muted-foreground flex items-center gap-3 rounded-md border p-3 text-sm">
            <ServerOffIcon className="size-5 shrink-0" />
            <span>{t("infobases.unassigned.dialog.unavailable")}</span>
          </div>
        )}

        {isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : showEmptyState ? (
          <div className="flex flex-col items-center justify-center gap-3 py-8 text-center">
            <DatabaseIcon className="text-muted-foreground size-8" />
            <div className="space-y-1">
              <p className="font-medium">{t("infobases.unassigned.dialog.empty.title")}</p>
              <p className="text-muted-foreground text-sm">
                {t("infobases.unassigned.dialog.empty.hint")}
              </p>
            </div>
          </div>
        ) : (
          available && (
            <ul className="divide-y rounded-md border">
              {items.map((item) => (
                <li
                  key={item.clusterInfobaseId}
                  className="flex flex-wrap items-center gap-x-3 gap-y-1 p-3"
                >
                  <DatabaseIcon className="text-muted-foreground size-4 shrink-0" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium" title={item.name}>
                      {item.name}
                    </p>
                    <p className={UUID_CLASS}>{item.clusterInfobaseId}</p>
                    {item.description && (
                      <p
                        className="text-muted-foreground truncate text-xs"
                        title={item.description}
                      >
                        {item.description}
                      </p>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={pending}
                      onClick={() => void handleHide(item)}
                    >
                      <EyeOffIcon className="size-4" />
                      {t("infobases.unassigned.actions.hide")}
                    </Button>
                    <Button size="sm" disabled={pending} onClick={() => onAssign(item)}>
                      {t("infobases.unassigned.actions.assign")}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )
        )}

        {hiddenItems.length > 0 && (
          <div className="space-y-2">
            <button
              type="button"
              onClick={() => setShowHidden((v) => !v)}
              aria-expanded={showHidden}
              className="text-muted-foreground hover:text-foreground text-sm font-medium"
            >
              {showHidden
                ? t("infobases.unassigned.hidden.hide")
                : t("infobases.unassigned.hidden.show", { count: hiddenItems.length })}
            </button>
            {showHidden && (
              <ul className="divide-y rounded-md border">
                {hiddenItems.map((hidden) => (
                  <li
                    key={hidden.clusterInfobaseId}
                    className="flex flex-wrap items-center gap-x-3 gap-y-1 p-3"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium" title={hidden.name}>
                        {hidden.name}
                      </p>
                      <p className={UUID_CLASS}>{hidden.clusterInfobaseId}</p>
                    </div>
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={pending}
                      onClick={() => void handleUnhide(hidden)}
                    >
                      <RotateCcwIcon className="size-4" />
                      {t("infobases.unassigned.actions.unhide")}
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        <Separator />

        <DialogFooter className="flex-col items-stretch gap-3 sm:flex-row sm:items-center sm:justify-between">
          <Button variant="link" className="h-auto justify-start px-0" onClick={onManualEntry}>
            {t("infobases.unassigned.dialog.manualEntry")}
          </Button>
          <div className="flex items-center gap-3">
            {checkedAtUtc && (
              <span className="text-muted-foreground text-xs">
                {t("infobases.unassigned.checkedAt")} <RelativeTime value={checkedAtUtc} />
              </span>
            )}
            <Button variant="ghost" size="sm" onClick={onRefresh} disabled={isRefreshing}>
              {t("common.refresh")}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
