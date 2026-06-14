import * as React from "react";
import { CheckIcon, ChevronDownIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

export interface SearchableSelectOption {
  value: string;
  label: string;
}

interface SearchableSelectProps {
  options: readonly SearchableSelectOption[];
  /** Текущее значение; `null` — выбран placeholder. */
  value: string | null;
  onChange: (value: string | null) => void;
  /** Подпись триггера, когда ничего не выбрано (и опция «любое» в списке). */
  placeholder: string;
  /** Текст плейсхолдера поля фильтра. */
  searchPlaceholder?: string;
  /** Текст пустого результата фильтрации. */
  emptyText?: string;
  className?: string;
  triggerClassName?: string;
  "aria-label"?: string;
}

/**
 * Переиспользуемый searchable-Select на Popover + Input + отфильтрованный список
 * кнопок (без новой зависимости). Триггер показывает выбранную подпись; в поповере —
 * поле фильтра по подстроке (case-insensitive) и список опций. Первая строка списка —
 * placeholder («любое»), сбрасывающий выбор в `null`. Выбор закрывает поповер.
 * Доступность: триггер — `role="combobox"` с `aria-expanded`; список — `role="listbox"`,
 * опции — `role="option"` с `aria-selected`; Esc закрывает (через Popover).
 */
export function SearchableSelect({
  options,
  value,
  onChange,
  placeholder,
  searchPlaceholder,
  emptyText,
  className,
  triggerClassName,
  "aria-label": ariaLabel,
}: SearchableSelectProps) {
  const { t } = useTranslation();
  const [open, setOpen] = React.useState(false);
  const [filter, setFilter] = React.useState("");

  const selected = value !== null ? options.find((o) => o.value === value) : undefined;
  const triggerLabel = selected?.label ?? placeholder;

  const normalized = filter.trim().toLowerCase();
  const filtered = normalized
    ? options.filter((o) => o.label.toLowerCase().includes(normalized))
    : options;

  const select = (next: string | null) => {
    onChange(next);
    setOpen(false);
    setFilter("");
  };

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setFilter("");
      }}
    >
      <PopoverTrigger asChild>
        <button
          type="button"
          role="combobox"
          aria-expanded={open}
          aria-haspopup="listbox"
          aria-label={ariaLabel}
          className={cn(
            "flex h-9 items-center justify-between gap-2 rounded-md border border-input bg-transparent px-3 py-2 text-sm whitespace-nowrap shadow-xs outline-none transition-[color,box-shadow] focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-input/30 dark:hover:bg-input/50",
            !selected && "text-muted-foreground",
            triggerClassName
          )}
        >
          <span className="line-clamp-1 text-left">{triggerLabel}</span>
          <ChevronDownIcon className="size-4 shrink-0 opacity-50" />
        </button>
      </PopoverTrigger>
      <PopoverContent className={cn("w-(--radix-popover-trigger-width) p-0", className)} align="start">
        <div className="border-b p-2">
          <Input
            autoFocus
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            placeholder={searchPlaceholder ?? t("common.search")}
            className="h-8"
            aria-label={searchPlaceholder ?? t("common.search")}
          />
        </div>
        <div role="listbox" className="max-h-72 overflow-y-auto p-1">
          <button
            type="button"
            role="option"
            aria-selected={value === null}
            onClick={() => select(null)}
            className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm outline-none hover:bg-accent hover:text-accent-foreground focus-visible:bg-accent focus-visible:text-accent-foreground"
          >
            <CheckIcon className={cn("size-4", value === null ? "opacity-100" : "opacity-0")} />
            <span className="line-clamp-1">{placeholder}</span>
          </button>
          {filtered.map((option) => {
            const isSelected = option.value === value;
            return (
              <button
                key={option.value}
                type="button"
                role="option"
                aria-selected={isSelected}
                onClick={() => select(option.value)}
                className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm outline-none hover:bg-accent hover:text-accent-foreground focus-visible:bg-accent focus-visible:text-accent-foreground"
              >
                <CheckIcon className={cn("size-4", isSelected ? "opacity-100" : "opacity-0")} />
                <span className="line-clamp-1">{option.label}</span>
              </button>
            );
          })}
          {filtered.length === 0 && (
            <p className="px-2 py-6 text-center text-sm text-muted-foreground">
              {emptyText ?? t("common.noData")}
            </p>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
