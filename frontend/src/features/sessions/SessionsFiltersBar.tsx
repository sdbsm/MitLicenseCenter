import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { InfobaseListItem } from "@/features/infobases/types";

interface SessionsFiltersBarProps {
  q: string;
  infobaseId: string;
  infobases: InfobaseListItem[];
  onChange: (next: { q?: string; infobaseId?: string }) => void;
}

/** Панель фильтров сессий: текстовый поиск (клиент/пользователь) + выбор инфобазы. */
export function SessionsFiltersBar({
  q,
  infobaseId,
  infobases,
  onChange,
}: SessionsFiltersBarProps) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-wrap gap-3">
      <Input
        className="w-72"
        placeholder={t("sessions.filters.search")}
        value={q}
        onChange={(e) => onChange({ q: e.target.value })}
      />
      <Select
        value={infobaseId || "_all"}
        onValueChange={(v) => onChange({ infobaseId: v === "_all" ? "" : v })}
      >
        <SelectTrigger className="w-52">
          <SelectValue placeholder={t("sessions.filters.allInfobases")} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all">{t("sessions.filters.allInfobases")}</SelectItem>
          {infobases.map((ib) => (
            <SelectItem key={ib.id} value={ib.id}>
              {ib.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
