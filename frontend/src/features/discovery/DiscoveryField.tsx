import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export interface DiscoveryOption {
  value: string;
  label: string;
  hint?: string | null;
}

interface DiscoveryFieldProps {
  value: string;
  onChange: (value: string) => void;
  options: DiscoveryOption[];
  available: boolean;
  loading: boolean;
  error?: string | null;
  onRefresh?: () => void;
  manualPlaceholder?: string;
  inputClassName?: string;
  // Предусловие не выполнено (например, не указан сервер БД) — только ручной ввод
  // с подсказкой; список не предлагается, пока предусловие не выполнится.
  disabledHint?: string | null;
}

// Поле «выбор из обнаруженного списка ИЛИ ручной ввод». Если источник недоступен,
// автоматически уходит в ручной режим (fallback). Контролируемый компонент —
// дружелюбен к react-hook-form (value/onChange приходят из field).
export function DiscoveryField({
  value,
  onChange,
  options,
  available,
  loading,
  error,
  onRefresh,
  manualPlaceholder,
  inputClassName,
  disabledHint,
}: DiscoveryFieldProps) {
  const { t } = useTranslation();
  const [manual, setManual] = useState(false);

  if (disabledHint) {
    return (
      <Input
        autoComplete="off"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={disabledHint}
        className={inputClassName}
      />
    );
  }

  const effectiveManual = manual || !available;

  if (effectiveManual) {
    return (
      <div className="space-y-1">
        <Input
          autoComplete="off"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={manualPlaceholder}
          className={inputClassName}
        />
        <div className="text-muted-foreground flex items-center gap-2 text-xs">
          {!available && error ? <span>{t("discovery.unavailable")}</span> : null}
          {available ? (
            <button type="button" className="underline" onClick={() => setManual(false)}>
              {t("discovery.chooseFromList")}
            </button>
          ) : onRefresh ? (
            <button type="button" className="underline" onClick={onRefresh} disabled={loading}>
              {loading ? t("discovery.loading") : t("discovery.retry")}
            </button>
          ) : null}
        </div>
      </div>
    );
  }

  // Если текущее значение не входит в обнаруженный список — добавляем его, чтобы
  // Select корректно отображал его (важно при редактировании существующей записи).
  const allOptions =
    value && !options.some((o) => o.value === value)
      ? [{ value, label: value } as DiscoveryOption, ...options]
      : options;

  return (
    <div className="space-y-1">
      <div className="flex items-center gap-2">
        <Select value={value || undefined} onValueChange={onChange} disabled={loading}>
          <SelectTrigger className="w-full">
            <SelectValue
              placeholder={loading ? t("discovery.loading") : t("discovery.selectPlaceholder")}
            />
          </SelectTrigger>
          <SelectContent>
            {allOptions.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.hint ? `${o.label} — ${o.hint}` : o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {onRefresh ? (
          <Button type="button" variant="outline" onClick={onRefresh} disabled={loading}>
            {t("discovery.refresh")}
          </Button>
        ) : null}
      </div>
      <div className="text-muted-foreground flex items-center gap-2 text-xs">
        {!loading && allOptions.length === 0 ? <span>{t("discovery.empty")}</span> : null}
        <button type="button" className="underline" onClick={() => setManual(true)}>
          {t("discovery.enterManually")}
        </button>
      </div>
    </div>
  );
}
