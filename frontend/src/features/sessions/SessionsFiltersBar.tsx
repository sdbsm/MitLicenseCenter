import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { SearchableSelect, type SearchableSelectOption } from "@/components/ui/SearchableSelect";
import { SearchableMultiSelect } from "@/components/ui/SearchableMultiSelect";
import { cn } from "@/lib/utils";
import type { InfobaseListItem } from "@/features/infobases/types";

interface SessionsFiltersBarProps {
  q: string;
  infobaseId: string;
  infobases: InfobaseListItem[];
  /** Опции фильтра «Тип сеанса» — присутствующие app-id с человеческими подписями. */
  appTypeOptions: SearchableSelectOption[];
  /** Текущий выбор типов сеансов (эффективный: дефолт-интерактивные либо явный). */
  selectedAppIds: string[];
  /** MLC-167: режим «Только лицензионные» (ВКЛ по умолчанию). */
  consuming: boolean;
  onChange: (next: {
    q?: string;
    infobaseId?: string;
    appIds?: string[];
    consuming?: boolean;
  }) => void;
}

/**
 * Панель фильтров сессий: текстовый поиск (клиент/пользователь) + searchable-выбор инфобазы
 * (UX-38) + мультивыбор типов сеансов по app-id (MLC-165) + тумблер «Только лицензионные»
 * (MLC-167). Когда тумблер ВКЛ — фильтр типов приглушён и отключён (значение сохраняется,
 * чтобы вернуться к нему при выключении тумблера), показываются только Consuming-сеансы.
 */
export function SessionsFiltersBar({
  q,
  infobaseId,
  infobases,
  appTypeOptions,
  selectedAppIds,
  consuming,
  onChange,
}: SessionsFiltersBarProps) {
  const { t } = useTranslation();

  const infobaseOptions: SearchableSelectOption[] = infobases.map((ib) => ({
    value: ib.id,
    label: ib.name,
  }));

  return (
    <div className="flex flex-wrap items-center gap-3">
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
      <Label className="gap-2">
        <Switch
          checked={consuming}
          onCheckedChange={(checked) => onChange({ consuming: checked })}
          aria-label={t("sessions.filters.consumingOnly")}
        />
        <span>{t("sessions.filters.consumingOnly")}</span>
      </Label>
      <div className={cn(consuming && "pointer-events-none opacity-50")} aria-disabled={consuming}>
        <SearchableMultiSelect
          options={appTypeOptions}
          value={selectedAppIds}
          onChange={(v) => onChange({ appIds: v })}
          placeholder={t("sessions.filters.allAppTypes")}
          selectedLabel={(count) => t("sessions.filters.appTypeSelected", { count })}
          searchPlaceholder={t("sessions.filters.searchAppType")}
          selectAllLabel={t("common.selectAll")}
          deselectAllLabel={t("common.deselectAll")}
          aria-label={t("sessions.filters.appType")}
          triggerClassName="w-52"
        />
      </div>
    </div>
  );
}
