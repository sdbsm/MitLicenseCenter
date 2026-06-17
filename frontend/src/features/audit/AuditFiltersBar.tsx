import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchableSelect, type SearchableSelectOption } from "@/components/ui/SearchableSelect";
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

const SEARCH_DEBOUNCE_MS = 300;

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

  // Локальный буфер строки поиска: набор не дёргает запрос на каждый символ,
  // коммит фильтра — после debounce. Синхронизируемся, если filters.search
  // изменился извне (reset / переход по shareable-ссылке).
  const [searchDraft, setSearchDraft] = useState(filters.search ?? "");
  const committedSearch = useRef(filters.search ?? "");
  useEffect(() => {
    if ((filters.search ?? "") !== committedSearch.current) {
      committedSearch.current = filters.search ?? "";
      setSearchDraft(filters.search ?? "");
    }
  }, [filters.search]);

  useEffect(() => {
    const next = searchDraft.trim() === "" ? null : searchDraft.trim();
    if ((next ?? "") === committedSearch.current) return;
    const id = setTimeout(() => {
      committedSearch.current = next ?? "";
      update({ search: next });
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchDraft]);

  const reset = () => {
    committedSearch.current = "";
    setSearchDraft("");
    onChange({
      actionType: null,
      tenantId: null,
      from: null,
      to: null,
      search: null,
      page: 1,
      pageSize: DEFAULT_AUDIT_PAGE_SIZE,
    });
  };

  const hasAnyFilter =
    filters.actionType !== null ||
    filters.tenantId !== null ||
    filters.from !== null ||
    filters.to !== null ||
    filters.search !== null;

  const actionOptions: SearchableSelectOption[] = AUDIT_ACTION_TYPES.map((action) => ({
    value: action,
    label: t(`audit.actions.${action}`),
  }));
  const tenantOptions: SearchableSelectOption[] = tenants.map((tenant) => ({
    value: tenant.id,
    label: tenant.name,
  }));

  return (
    <div className="bg-muted/30 flex flex-wrap items-end gap-3 rounded-md border p-3">
      <div className="grid gap-1.5">
        <Label className="text-xs font-medium">{t("audit.filters.actionType")}</Label>
        <SearchableSelect
          options={actionOptions}
          value={filters.actionType}
          onChange={(value) => update({ actionType: (value as AuditActionType) ?? null })}
          placeholder={t("audit.filters.anyAction")}
          searchPlaceholder={t("audit.filters.searchAction")}
          aria-label={t("audit.filters.actionType")}
          triggerClassName="w-60"
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium">{t("audit.filters.tenant")}</Label>
        <SearchableSelect
          options={tenantOptions}
          value={filters.tenantId}
          onChange={(value) => update({ tenantId: value })}
          placeholder={t("audit.filters.anyTenant")}
          searchPlaceholder={t("audit.filters.searchTenant")}
          aria-label={t("audit.filters.tenant")}
          triggerClassName="w-60"
        />
      </div>

      <div className="grid gap-1.5">
        <Label className="text-xs font-medium" htmlFor="audit-search">
          {t("audit.filters.search")}
        </Label>
        <Input
          id="audit-search"
          type="search"
          className="w-60"
          placeholder={t("audit.filters.searchPlaceholder")}
          value={searchDraft}
          onChange={(e) => setSearchDraft(e.target.value)}
        />
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
          onValueChange={(value) => update({ pageSize: Number(value) as AuditPageSize, page: 1 })}
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
