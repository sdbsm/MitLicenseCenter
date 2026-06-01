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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import type { Tenant } from "@/features/tenants/types";
import type { InfobaseListItem } from "./types";
import { useReassignInfobase } from "./useInfobases";

interface ConflictBody {
  code?: string;
}

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
      if (err instanceof ApiError && err.status === 409) {
        const body = err.body as ConflictBody | null;
        if (body?.code === "INFOBASE_NAME_TAKEN_IN_TARGET") {
          setError(t("infobases.reassign.nameTaken"));
          return;
        }
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
          <Label htmlFor="reassign-target">{t("infobases.reassign.targetLabel")}</Label>
          <Select
            value={targetTenantId}
            onValueChange={(v) => {
              setTargetTenantId(v);
              setError(null);
            }}
          >
            <SelectTrigger id="reassign-target" className="w-full">
              <SelectValue placeholder={t("infobases.reassign.targetPlaceholder")} />
            </SelectTrigger>
            <SelectContent>
              {candidates.map((tnt) => (
                <SelectItem key={tnt.id} value={tnt.id}>
                  {tnt.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
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
