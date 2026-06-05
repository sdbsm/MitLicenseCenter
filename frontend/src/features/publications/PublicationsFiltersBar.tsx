import { useTranslation } from "react-i18next";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { Tenant } from "@/features/tenants/types";
import type { PublicationPublishStatus } from "./types";
import { PUBLISH_STATUSES, type UrlFilters } from "./urlState";

interface PublicationsFiltersBarProps {
  tenants: Tenant[];
  tenantId: string;
  status: string;
  onChange: (next: Partial<UrlFilters>) => void;
}

/** Панель фильтров публикаций: выбор клиента + статуса публикации. */
export function PublicationsFiltersBar({
  tenants,
  tenantId,
  status,
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
        value={status || "_all"}
        onValueChange={(v) =>
          onChange({ status: v === "_all" ? "" : (v as PublicationPublishStatus) })
        }
      >
        <SelectTrigger className="w-52">
          <SelectValue placeholder={t("publications.filters.status")} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all">{t("publications.filters.all")}</SelectItem>
          {PUBLISH_STATUSES.map((s) => (
            <SelectItem key={s} value={s}>
              {t(`publications.status.${s.toLowerCase()}`)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
