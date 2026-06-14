import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { Badge } from "./badge";

export type StatusBadgeVariant = "success" | "warning" | "danger" | "info" | "neutral";

const variantClass: Record<StatusBadgeVariant, string> = {
  success: "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
  // amber-800 в светлой теме: 6.32:1 WCAG AA (amber-700 давал 4.48:1, ниже порога) — MLC-138/UX-28
  warning: "border-transparent bg-amber-500/15 text-amber-800 dark:text-amber-300",
  danger: "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300",
  info: "border-transparent bg-sky-500/15 text-sky-700 dark:text-sky-300",
  // zinc-700/zinc-400 вместо muted-foreground: 7.09:1 light / 5.90:1 dark (MLC-138/UX-28)
  // muted-foreground давал 4.34:1 в светлой теме, ниже WCAG AA 4.5:1
  neutral: "border-transparent bg-muted text-zinc-700 dark:text-zinc-400",
};

interface StatusBadgeProps {
  variant: StatusBadgeVariant;
  children: ReactNode;
  className?: string;
}

export function StatusBadge({ variant, children, className }: StatusBadgeProps) {
  // data-variant — стабильный наблюдаемый признак выбранного варианта (FE-19, MLC-120):
  // тесты привязываются к семантике status→variant, а не к Tailwind-классам (которые
  // меняются при дизайн-рефакторе и давали ложно-красный). Поведение рендера не меняется.
  return (
    <Badge
      data-variant={variant}
      className={cn(variantClass[variant], className)}
      variant="outline"
    >
      {children}
    </Badge>
  );
}
