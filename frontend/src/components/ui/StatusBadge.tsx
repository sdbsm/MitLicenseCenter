import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { Badge } from "./badge";

export type StatusBadgeVariant = "success" | "warning" | "danger" | "info" | "neutral";

const variantClass: Record<StatusBadgeVariant, string> = {
  success: "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300",
  warning: "border-transparent bg-amber-500/15 text-amber-700 dark:text-amber-300",
  danger: "border-transparent bg-rose-500/15 text-rose-700 dark:text-rose-300",
  info: "border-transparent bg-sky-500/15 text-sky-700 dark:text-sky-300",
  neutral: "border-transparent bg-muted text-muted-foreground",
};

interface StatusBadgeProps {
  variant: StatusBadgeVariant;
  children: ReactNode;
  className?: string;
}

export function StatusBadge({ variant, children, className }: StatusBadgeProps) {
  return (
    <Badge className={cn(variantClass[variant], className)} variant="outline">
      {children}
    </Badge>
  );
}
