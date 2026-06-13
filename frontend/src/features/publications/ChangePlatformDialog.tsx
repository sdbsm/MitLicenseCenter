import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePlatformVersions } from "@/features/discovery/useDiscovery";
import { ApiError } from "@/lib/api";
import type { PublicationListItem } from "./types";
import { useChangePlatform } from "./usePublications";

interface ConflictBody {
  code?: string;
  detail?: string;
  title?: string;
}

interface ChangePlatformDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  publication: PublicationListItem | null;
}

// Диалог смены платформы (MLC-045): выбор установленной версии → правка пути к
// wsisapi.dll в web.config. default.vrd не трогается.
export function ChangePlatformDialog({
  open,
  onOpenChange,
  publication,
}: ChangePlatformDialogProps) {
  const { t } = useTranslation();
  const [version, setVersion] = useState("");
  const changePlatform = useChangePlatform();
  const platforms = usePlatformVersions(open);

  if (!publication) {
    return null;
  }

  const options = platforms.data?.items ?? [];

  const handleConfirm = async () => {
    try {
      await changePlatform.mutateAsync({ id: publication.id, platformVersion: version });
      toast.success(t("publications.toasts.platformChanged"));
      onOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        const body = error.body as ConflictBody | null;
        const detail = body?.detail ?? body?.title ?? t("publications.toasts.platformChangeFailed");
        toast.error(detail);
        return;
      }
      if (error instanceof ApiError && error.status === 400) {
        // UX-04 — версия выбирается из Select (нет текстового поля для inline-ошибки),
        // поэтому показываем конкретное серверное сообщение 400 ValidationProblem
        // вместо немого generic-текста.
        const body = error.body as { errors?: Record<string, string[]> } | null;
        const fieldMessage = body?.errors ? Object.values(body.errors)[0]?.[0] : undefined;
        toast.error(fieldMessage ?? error.message ?? t("publications.toasts.platformChangeFailed"));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("publications.changePlatform.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("publications.changePlatform.body", {
              siteName: publication.siteName,
              virtualPath: publication.virtualPath,
              current: publication.platformVersion,
            })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="grid gap-2">
          <Label className="text-sm">{t("publications.changePlatform.versionLabel")}</Label>
          <Select value={version} onValueChange={setVersion}>
            <SelectTrigger>
              <SelectValue placeholder={t("publications.changePlatform.versionPlaceholder")} />
            </SelectTrigger>
            <SelectContent>
              {options.map((p) => (
                <SelectItem key={p.version} value={p.version}>
                  {p.version}
                  {p.architecture ? ` (${p.architecture})` : ""}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel disabled={changePlatform.isPending}>
            {t("publications.changePlatform.cancel")}
          </AlertDialogCancel>
          <AlertDialogAction
            disabled={!version || changePlatform.isPending}
            onClick={(e) => {
              e.preventDefault();
              void handleConfirm();
            }}
          >
            {changePlatform.isPending
              ? t("common.loading")
              : t("publications.changePlatform.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
