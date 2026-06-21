import { useMemo, useState } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import {
  useInvestigations,
  useInvestigationProgress,
} from "@/features/investigations/useInvestigations";
import { type InvestigationSummary } from "@/features/investigations/types";
import { InvestigationDetail } from "./InvestigationDetail";
import { InvestigationList } from "./InvestigationList";
import { InvestigationWizard } from "./InvestigationWizard";
import { InvestigationProgress } from "./InvestigationProgress";

/**
 * Режим «Расследование» — точка входа воронки (MLC-242/243, ADR-57).
 *
 * Под-состояния (хранятся локально — маршрут остаётся /performance):
 *   • "list"       — Список дел (экран 5, ДЕФОЛТ при отсутствии активного сбора).
 *   • "wizard"     — Мастер запуска нового расследования (экран 2).
 *   • "progress"   — Прогресс активного сбора (экран 6), приоритет над "list".
 *   • "detail(id)" — Карточка «Дело» (экран 3).
 *
 * Логика приоритетов:
 *   1. Если есть активное дело (Collecting / Analyzing) И подсостояние не "detail" —
 *      форсируем "progress" (баннер в списке ведёт сюда напрямую).
 *   2. Иначе — показываем текущее подсостояние (list / wizard / detail).
 *
 * Документ-«Отчёт» (экран 4) — MLC-244 (кнопка «Отчёт» в деталях = disabled-заглушка).
 */

type SubView =
  | { kind: "list" }
  | { kind: "wizard" }
  | { kind: "progress" }
  | { kind: "detail"; id: string };

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

  // Под-состояние (дефолт — список)
  const [view, setView] = useState<SubView>({ kind: "list" });

  if (isLoading && !data) {
    return <Skeleton className="h-64 w-full" />;
  }

  // Приоритет прогресса: есть активное дело и мы не смотрим на конкретную деталь → Прогресс.
  // Мастер достижим только из списка (а список — лишь когда активного нет), поэтому застрять
  // на Мастере с активным делом нельзя: после старта `onStarted` уводит на список, и активное
  // (только что созданное) дело форсирует Прогресс здесь же.
  if (activeSummary && view.kind !== "detail") {
    return <InvestigationProgress summary={activeSummary} progress={progressData ?? null} />;
  }

  if (view.kind === "wizard") {
    return (
      <InvestigationWizard
        onCancel={() => setView({ kind: "list" })}
        onStarted={() => setView({ kind: "list" })}
      />
    );
  }

  if (view.kind === "detail") {
    return (
      <InvestigationDetail investigationId={view.id} onBack={() => setView({ kind: "list" })} />
    );
  }

  // Дефолт: список (kind === "list")
  return (
    <InvestigationList
      onNewInvestigation={() => setView({ kind: "wizard" })}
      onSelectInvestigation={(id) => setView({ kind: "detail", id })}
      onShowProgress={() => setView({ kind: "progress" })}
    />
  );
}
