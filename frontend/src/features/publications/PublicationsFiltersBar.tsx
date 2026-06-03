import { useTranslation } from "react-i18next";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { Tenant } from "@/features/tenants/types";
import type { PublicationDriftStatus } from "./types";
import { DRIFT_STATUSES, type UrlFilters } from "./urlState";

interface PublicationsFiltersBarProps {
  tenants: Tenant[];
  tenantId: string;
  driftStatus: string;
  onChange: (next: Partial<UrlFilters>) => void;
}

/** Панель фильтров публикаций: выбор клиента + статуса дрейфа. */
export function PublicationsFiltersBar({
  tenants,
  tenantId,
  driftStatus,
  onChange,
}: PublicationsFiltersBarProps) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-wrap gap-3">
      <Select
        value={tenantId || "_all"}
        onValueChange={(v) => onChange({ tenantId: v === "_all" ? "" : v })}
      >
        <SelectTrigger className="w-60">
          <SelectValue placeholder={t("publications.filters.tenant")} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all">{t("publications.filters.all")}</SelectItem>
          {tenants.map((tenant) => (
            <SelectItem key={tenant.id} value={tenant.id}>
              {tenant.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select
        value={driftStatus || "_all"}
        onValueChange={(v) =>
          onChange({ driftStatus: v === "_all" ? "" : (v as PublicationDriftStatus) })
        }
      >
        <SelectTrigger className="w-52">
          <SelectValue placeholder={t("publications.filters.driftStatus")} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all">{t("publications.filters.all")}</SelectItem>
          {DRIFT_STATUSES.map((status) => (
            <SelectItem key={status} value={status}>
              {t(`publications.driftStatus.${status.toLowerCase()}`)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
