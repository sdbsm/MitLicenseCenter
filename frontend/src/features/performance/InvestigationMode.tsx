import { useMemo } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import {
  useInvestigations,
  useInvestigationProgress,
} from "@/features/investigations/useInvestigations";
import { type InvestigationSummary } from "@/features/investigations/types";
import { InvestigationWizard } from "./InvestigationWizard";
import { InvestigationProgress } from "./InvestigationProgress";

/**
 * Режим «Расследование» — точка входа воронки (MLC-242, ADR-57).
 *
 * Логика:
 *   • Если есть активное дело (статус Collecting / Analyzing) → показываем Прогресс (экран 6).
 *   • Иначе → показываем Мастер запуска (экран 2).
 *
 * Полноценный «Список дел» (экран 5), карточка дела (экран 3) и отчёт (экран 4) — MLC-243/244.
 */

/** Вспомогательный хук: поллит прогресс только при наличии активного id. */
function useActiveProgress(activeId: string | null) {
  return useInvestigationProgress(activeId);
}

export function InvestigationMode() {
  const { data, isLoading } = useInvestigations();

  // Активное дело: первое с Collecting или Analyzing
  const activeSummary = useMemo<InvestigationSummary | null>(() => {
    if (!data) return null;
    return (
      data.items.find((inv) => inv.status === "Collecting" || inv.status === "Analyzing") ?? null
    );
  }, [data]);

  const { data: progressData } = useActiveProgress(activeSummary?.id ?? null);

  if (isLoading && !data) {
    return <Skeleton className="h-64 w-full" />;
  }

  if (activeSummary) {
    return <InvestigationProgress summary={activeSummary} progress={progressData ?? null} />;
  }

  return <InvestigationWizard />;
}
