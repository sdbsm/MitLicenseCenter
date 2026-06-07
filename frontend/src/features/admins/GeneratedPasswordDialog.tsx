import { CheckIcon, CopyIcon } from "lucide-react";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface GeneratedPasswordDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userName: string;
  password: string;
}

// Показ временного пароля один раз после создания/сброса. Пароль нигде не хранится и
// больше не отображается — операнду нужно скопировать его здесь и передать пользователю.
export function GeneratedPasswordDialog({
  open,
  onOpenChange,
  userName,
  password,
}: GeneratedPasswordDialogProps) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
      toast.success(t("common.copied"));
    } catch {
      toast.error(t("admins.password.copyFailed"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("admins.password.title")}</DialogTitle>
          <DialogDescription>{t("admins.password.body", { name: userName })}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          <Label htmlFor="generated-password">{t("admins.password.label")}</Label>
          <div className="flex items-center gap-2">
            <Input
              id="generated-password"
              readOnly
              value={password}
              className="font-mono"
              onFocus={(e) => e.target.select()}
            />
            <Button type="button" variant="outline" size="icon" onClick={handleCopy}>
              {copied ? <CheckIcon className="size-4" /> : <CopyIcon className="size-4" />}
              <span className="sr-only">{t("common.copy")}</span>
            </Button>
          </div>
          <p className="text-muted-foreground text-xs">{t("admins.password.oneTimeWarning")}</p>
        </div>

        <DialogFooter>
          <Button type="button" onClick={() => onOpenChange(false)}>
            {t("common.close")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
