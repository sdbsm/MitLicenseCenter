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
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/SearchableSelect";
import { matchConflictCode } from "@/lib/apiErrors";
import type { Tenant } from "@/features/tenants/types";
import type { InfobaseListItem } from "./types";
import { useReassignInfobase } from "./useInfobases";

interface ReassignInfobaseDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  infobase: InfobaseListItem | null;
  tenants: Tenant[];
}

export function ReassignInfobaseDialog({
  open,
  onOpenChange,
  infobase,
  tenants,
}: ReassignInfobaseDialogProps) {
  const { t } = useTranslation();
  const [targetTenantId, setTargetTenantId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const reassign = useReassignInfobase();

  if (!infobase) {
    return null;
  }

  const candidates = tenants.filter((tnt) => tnt.id !== infobase.tenantId);

  const handleConfirm = async () => {
    if (!targetTenantId) return;
    setError(null);
    try {
      await reassign.mutateAsync({ id: infobase.id, targetTenantId });
      const targetName = candidates.find((c) => c.id === targetTenantId)?.name ?? "";
      toast.success(t("infobases.reassign.success", { name: infobase.name, tenant: targetName }));
      onOpenChange(false);
    } catch (err) {
      const messageKey = matchConflictCode(err, {
        INFOBASE_NAME_TAKEN_IN_TARGET: "infobases.reassign.nameTaken",
      });
      if (messageKey) {
        setError(t(messageKey));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("infobases.reassign.title")}</DialogTitle>
          <DialogDescription>
            {t("infobases.reassign.body", { name: infobase.name })}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          <Label>{t("infobases.reassign.targetLabel")}</Label>
          <SearchableSelect
            options={candidates.map((tnt) => ({ value: tnt.id, label: tnt.name }))}
            value={targetTenantId || null}
            onChange={(v) => {
              setTargetTenantId(v ?? "");
              setError(null);
            }}
            placeholder={t("infobases.reassign.targetPlaceholder")}
            searchPlaceholder={t("common.search")}
            aria-label={t("infobases.reassign.targetLabel")}
            triggerClassName="w-full"
          />
          {error && <p className="text-destructive text-sm">{error}</p>}
        </div>

        <DialogFooter className="gap-2">
          <Button
            type="button"
            variant="ghost"
            disabled={reassign.isPending}
            onClick={() => onOpenChange(false)}
          >
            {t("common.cancel")}
          </Button>
          <Button
            type="button"
            disabled={!targetTenantId || reassign.isPending}
            onClick={() => void handleConfirm()}
          >
            {reassign.isPending ? t("common.loading") : t("infobases.reassign.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
