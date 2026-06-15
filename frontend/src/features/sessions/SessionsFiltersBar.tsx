import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import { SearchableSelect, type SearchableSelectOption } from "@/components/ui/SearchableSelect";
import { SearchableMultiSelect } from "@/components/ui/SearchableMultiSelect";
import type { InfobaseListItem } from "@/features/infobases/types";

interface SessionsFiltersBarProps {
  q: string;
  infobaseId: string;
  infobases: InfobaseListItem[];
  /** Опции фильтра «Тип сеанса» — присутствующие app-id с человеческими подписями. */
  appTypeOptions: SearchableSelectOption[];
  /** Текущий выбор типов сеансов (эффективный: дефолт-интерактивные либо явный). */
  selectedAppIds: string[];
  onChange: (next: { q?: string; infobaseId?: string; appIds?: string[] }) => void;
}

/**
 * Панель фильтров сессий: текстовый поиск (клиент/пользователь) + searchable-выбор инфобазы
 * (UX-38) + мультивыбор типов сеансов по app-id (MLC-165).
 */
export function SessionsFiltersBar({
  q,
  infobaseId,
  infobases,
  appTypeOptions,
  selectedAppIds,
  onChange,
}: SessionsFiltersBarProps) {
  const { t } = useTranslation();

  const infobaseOptions: SearchableSelectOption[] = infobases.map((ib) => ({
    value: ib.id,
    label: ib.name,
  }));

  return (
    <div className="flex flex-wrap gap-3">
      <Input
        className="w-72"
        placeholder={t("sessions.filters.search")}
        value={q}
        onChange={(e) => onChange({ q: e.target.value })}
      />
      <SearchableSelect
        options={infobaseOptions}
        value={infobaseId || null}
        onChange={(v) => onChange({ infobaseId: v ?? "" })}
        placeholder={t("sessions.filters.allInfobases")}
        searchPlaceholder={t("sessions.filters.searchInfobase")}
        aria-label={t("sessions.filters.infobase")}
        triggerClassName="w-52"
      />
      <SearchableMultiSelect
        options={appTypeOptions}
        value={selectedAppIds}
        onChange={(v) => onChange({ appIds: v })}
        placeholder={t("sessions.filters.allAppTypes")}
        selectedLabel={(count) => t("sessions.filters.appTypeSelected", { count })}
        searchPlaceholder={t("sessions.filters.searchAppType")}
        aria-label={t("sessions.filters.appType")}
        triggerClassName="w-52"
      />
    </div>
  );
}
