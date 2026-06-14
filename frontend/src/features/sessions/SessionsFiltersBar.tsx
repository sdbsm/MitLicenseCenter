import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import { SearchableSelect, type SearchableSelectOption } from "@/components/ui/SearchableSelect";
import type { InfobaseListItem } from "@/features/infobases/types";

interface SessionsFiltersBarProps {
  q: string;
  infobaseId: string;
  infobases: InfobaseListItem[];
  onChange: (next: { q?: string; infobaseId?: string }) => void;
}

/** Панель фильтров сессий: текстовый поиск (клиент/пользователь) + searchable-выбор инфобазы (UX-38). */
export function SessionsFiltersBar({
  q,
  infobaseId,
  infobases,
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
    </div>
  );
}
