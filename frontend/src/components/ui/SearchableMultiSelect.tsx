import * as React from "react";
import { CheckIcon, ChevronDownIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";
import type { SearchableSelectOption } from "@/components/ui/SearchableSelect";

interface SearchableMultiSelectProps {
  options: readonly SearchableSelectOption[];
  /** Текущий выбор (множество значений). Пустой массив — ничего не выбрано. */
  value: string[];
  onChange: (value: string[]) => void;
  /** Подпись триггера, когда ничего не выбрано. */
  placeholder: string;
  /**
   * Подпись триггера при N выбранных. Получает количество; должна содержать число.
   * Если не передана — подставляется первая из выбранных подписей + «+N».
   */
  selectedLabel?: (count: number) => string;
  /** Текст плейсхолдера поля фильтра. */
  searchPlaceholder?: string;
  /** Текст пустого результата фильтрации. */
  emptyText?: string;
  className?: string;
  triggerClassName?: string;
  "aria-label"?: string;
}

/**
 * Переиспользуемый searchable-мультивыбор на Popover + Input + отфильтрованный список
 * чекбокс-кнопок (зеркало `SearchableSelect`, те же примитивы shadcn). Триггер показывает
 * «выбрано N» (или плейсхолдер при пустом выборе); в поповере — поле фильтра по подстроке
 * (case-insensitive) и список опций-тогглов. Выбор НЕ закрывает поповер (мультивыбор).
 * Доступность: триггер — `role="combobox"` с `aria-expanded`; список — `role="listbox"`
 * с `aria-multiselectable`, опции — `role="option"` с `aria-selected`; Esc закрывает.
 */
export function SearchableMultiSelect({
  options,
  value,
  onChange,
  placeholder,
  selectedLabel,
  searchPlaceholder,
  emptyText,
  className,
  triggerClassName,
  "aria-label": ariaLabel,
}: SearchableMultiSelectProps) {
  const { t } = useTranslation();
  const [open, setOpen] = React.useState(false);
  const [filter, setFilter] = React.useState("");

  const selectedSet = React.useMemo(() => new Set(value), [value]);

  const triggerLabel =
    value.length === 0 ? placeholder : (selectedLabel?.(value.length) ?? `${value.length}`);

  const normalized = filter.trim().toLowerCase();
  const filtered = normalized
    ? options.filter((o) => o.label.toLowerCase().includes(normalized))
    : options;

  const toggle = (optionValue: string) => {
    if (selectedSet.has(optionValue)) {
      onChange(value.filter((v) => v !== optionValue));
    } else {
      onChange([...value, optionValue]);
    }
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
            value.length === 0 && "text-muted-foreground",
            triggerClassName
          )}
        >
          <span className="line-clamp-1 text-left">{triggerLabel}</span>
          <ChevronDownIcon className="size-4 shrink-0 opacity-50" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        className={cn("w-(--radix-popover-trigger-width) p-0", className)}
        align="start"
      >
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
        <div role="listbox" aria-multiselectable className="max-h-72 overflow-y-auto p-1">
          {filtered.map((option) => {
            const isSelected = selectedSet.has(option.value);
            return (
              <button
                key={option.value}
                type="button"
                role="option"
                aria-selected={isSelected}
                onClick={() => toggle(option.value)}
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
