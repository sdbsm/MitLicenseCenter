import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useTenants } from "@/features/tenants/useTenants";
import {
  AUDIT_ACTION_TYPES,
  AUDIT_PAGE_SIZES,
  type AuditActionType,
  type AuditFilters,
  type AuditPageSize,
  DEFAULT_AUDIT_PAGE_SIZE,
} from "./types";

const ANY_ACTION = "__any_action__";
const ANY_TENANT = "__any_tenant__";

interface AuditFiltersBarProps {
  filters: AuditFilters;
  onChange: (next: AuditFilters) => void;
}

export function AuditFiltersBar({ filters, onChange }: AuditFiltersBarProps) {
  const { t } = useTranslation();
  const { data: tenantsData } = useTenants();
  const tenants = tenantsData?.items ?? [];

  const update = (patch: Partial<AuditFilters>) => {
    // Любое изменение фильтра возвращает пагинацию на первую страницу — иначе
    // вторая страница может оказаться пустой и UI «теряется».
    onChange({ ...filters, ...patch, page: patch.page ?? 1 });
  };

  const reset = () => {
    onChange({
      actionType: null,
      tenantId: null,
      from: null,
      to: null,
      page: 1,
      pageSize: DEFAULT_AUDIT_PAGE_SIZE,
    });
  };

  const hasAnyFilter =
    filters.actionType !== null ||
    filters.tenantId !== null ||
    filters.from !== null ||
    filters.to !== null;

  return (
    <div className="flex flex-wrap items-end gap-3 rounded-md border bg-muted/30 p-3">
      <div className="grid gap-1.5">
        <Label className="text-xs font-medium">{t("audit.filters.actionType")}</Label>
        <Select
          value={filters.actionType ?? ANY_ACTION}
          onValueChange={(value) =>
            update({
              actionType: value === ANY_ACTION ? null : (value as AuditActionType),
            })
          }
        >
          <SelectTrigger className="w-60">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ANY_ACTION}>{t("audit.filters.anyAction")}</SelectItem>
            {AUDIT_ACTION_TYPES.map((action) => (
              <SelectItem key={action} value={action}>
                {t(`audit.actions.${action}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium">{t("audit.filters.tenant")}</Label>
        <Select
          value={filters.tenantId ?? ANY_TENANT}
          onValueChange={(value) =>
            update({ tenantId: value === ANY_TENANT ? null : value })
          }
        >
          <SelectTrigger className="w-60">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ANY_TENANT}>{t("audit.filters.anyTenant")}</SelectItem>
            {tenants.map((tenant) => (
              <SelectItem key={tenant.id} value={tenant.id}>
                {tenant.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="audit-from">
          {t("audit.filters.from")}
        </Label>
        <Input
          id="audit-from"
          type="date"
          className="w-40"
          value={filters.from ?? ""}
          onChange={(e) => update({ from: e.target.value || null })}
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="audit-to">
          {t("audit.filters.to")}
        </Label>
        <Input
          id="audit-to"
          type="date"
          className="w-40"
          value={filters.to ?? ""}
          onChange={(e) => update({ to: e.target.value || null })}
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium">{t("audit.filters.pageSize")}</Label>
        <Select
          value={String(filters.pageSize)}
          onValueChange={(value) =>
            update({ pageSize: Number(value) as AuditPageSize, page: 1 })
          }
        >
          <SelectTrigger className="w-24">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {AUDIT_PAGE_SIZES.map((size) => (
              <SelectItem key={size} value={String(size)}>
                {size}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {hasAnyFilter && (
        <Button variant="ghost" size="sm" onClick={reset}>
          {t("common.reset")}
        </Button>
      )}
    </div>
  );
}
