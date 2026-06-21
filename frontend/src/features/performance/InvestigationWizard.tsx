import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { AlertCircleIcon, ShieldCheckIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/SearchableSelect";
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import { matchConflictCode } from "@/lib/apiErrors";
import { ApiError } from "@/lib/api";
import { type InvestigationScenario } from "@/features/investigations/types";
import { useStartInvestigation } from "@/features/investigations/useInvestigations";

/**
 * Мастер запуска расследования производительности — экран 2 (MLC-242, ADR-57, спека §Экран 2).
 *
 * Форма выбора сценария + scope (весь узел / конкретная ИБ) + плашка безопасности.
 * Лимиты сбора — ГЛОБАЛЬНЫЕ настройки панели (TechLog.*, ADR-58/MLC-231), не пер-сбор:
 * здесь они не редактируемые поля, а информационная плашка безопасности.
 * Управление — только Admin (Viewer видит disabled-кнопку с подсказкой).
 */

/** Четыре пользовательских сценария мастера (спека §Экран 2). DbmsLocks — не показываем. */
const WIZARD_SCENARIOS: InvestigationScenario[] = [
  "Locks",
  "SlowQueries",
  "Exceptions",
  "GeneralSlow",
];

type ScopeType = "all" | "infobase";

interface InvestigationWizardProps {
  onCancel?: () => void;
}

export function InvestigationWizard({ onCancel }: InvestigationWizardProps) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  const [scenario, setScenario] = useState<InvestigationScenario | "">("");
  const [scope, setScope] = useState<ScopeType>("all");
  const [infobaseId, setInfobaseId] = useState<string | null>(null);
  const [inlineError, setInlineError] = useState<string | null>(null);

  // Список инфобаз для scope-дропдауна: одна страница с запасом
  const { data: infobasesData } = useInfobases(
    null, // tenantId
    null, // publishStatus
    false, // notInCluster
    1, // page
    100 // pageSize
  );
  const infobases = infobasesData?.items ?? [];

  const startInvestigation = useStartInvestigation();

  const handleStart = async () => {
    if (!scenario) return;
    setInlineError(null);

    const body = scope === "infobase" && infobaseId ? { scenario, infobaseId } : { scenario };

    try {
      await startInvestigation.mutateAsync(body);
      // После успешного старта список инвалидируется хуком → режим переключится на Прогресс.
    } catch (error) {
      const conflictKey = matchConflictCode(error, {
        INVESTIGATION_ACTIVE: "performance.investigation.wizard.errors.INVESTIGATION_ACTIVE",
      });
      if (conflictKey) {
        setInlineError(t(conflictKey));
        return;
      }

      // INVESTIGATION_START_FAILED: detail несёт причину (icacls и т.п.)
      if (error instanceof ApiError && error.status === 409) {
        const detail = error.message || "";
        setInlineError(
          t("performance.investigation.wizard.errors.INVESTIGATION_START_FAILED", { detail })
        );
        return;
      }

      toast.error(t("errors.generic"));
    }
  };

  const canStart =
    isAdmin &&
    scenario !== "" &&
    (scope === "all" || (scope === "infobase" && infobaseId !== null));

  const infobaseOptions = infobases.map((ib) => ({
    value: ib.id,
    label: `${ib.name} — ${ib.tenantName}`,
  }));

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("performance.investigation.wizard.title")}</CardTitle>
        <p className="text-muted-foreground text-sm">
          {t("performance.investigation.wizard.subtitle")}
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* 1. Сценарий */}
        <div className="space-y-2">
          <Label htmlFor="wizard-scenario">
            {t("performance.investigation.wizard.scenarioLabel")}
          </Label>
          <Select value={scenario} onValueChange={(v) => setScenario(v as InvestigationScenario)}>
            <SelectTrigger id="wizard-scenario" className="w-full">
              <SelectValue
                placeholder={t("performance.investigation.wizard.scenarioPlaceholder")}
              />
            </SelectTrigger>
            <SelectContent>
              {WIZARD_SCENARIOS.map((s) => (
                <SelectItem key={s} value={s}>
                  {t(`investigations.scenario.${s}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* 2. Scope — нативные radio (radio-group в проекте не установлен) */}
        <fieldset className="space-y-3">
          <legend className="text-sm leading-none font-medium">
            {t("performance.investigation.wizard.scopeLabel")}
          </legend>

          <label className="flex cursor-pointer items-center gap-2 text-sm font-normal">
            <input
              type="radio"
              name="investigation-scope"
              value="all"
              checked={scope === "all"}
              onChange={() => {
                setScope("all");
                setInfobaseId(null);
              }}
              className="accent-primary"
            />
            {t("performance.investigation.wizard.scopeAll")}
          </label>

          <label className="flex cursor-pointer items-center gap-2 text-sm font-normal">
            <input
              type="radio"
              name="investigation-scope"
              value="infobase"
              checked={scope === "infobase"}
              onChange={() => setScope("infobase")}
              className="accent-primary"
            />
            {t("performance.investigation.wizard.scopeInfobase")}
          </label>

          {scope === "infobase" && (
            <div className="ml-6 space-y-2">
              <SearchableSelect
                options={infobaseOptions}
                value={infobaseId}
                onChange={(v) => setInfobaseId(v)}
                placeholder={t("performance.investigation.wizard.infobasePlaceholder")}
                searchPlaceholder={t("performance.investigation.wizard.infobaseSearchPlaceholder")}
                emptyText={t("performance.investigation.wizard.infobaseEmpty")}
                aria-label={t("performance.investigation.wizard.infobaseLabel")}
                triggerClassName="w-full"
              />
              <p className="text-muted-foreground text-xs">
                {t("performance.investigation.wizard.infobaseScopeHint")}
              </p>
            </div>
          )}
        </fieldset>

        {/* 3. Плашка безопасности (информационно; лимиты — глобальные настройки панели ADR-58) */}
        <div className="bg-muted/60 flex gap-3 rounded-lg p-4">
          <ShieldCheckIcon
            className="text-muted-foreground mt-0.5 size-4 shrink-0"
            aria-hidden="true"
          />
          <div className="space-y-1">
            <p className="text-sm font-medium">
              {t("performance.investigation.wizard.safetyTitle")}
            </p>
            <p className="text-muted-foreground text-xs leading-relaxed">
              {t("performance.investigation.wizard.safetyText")}
            </p>
          </div>
        </div>

        {/* Inline-ошибка (409 конфликты) */}
        {inlineError && (
          <div className="flex items-start gap-2 text-sm text-rose-600 dark:text-rose-400">
            <AlertCircleIcon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>{inlineError}</span>
          </div>
        )}

        {/* Подсказка для Viewer */}
        {!isAdmin && (
          <p className="text-muted-foreground text-xs">
            {t("performance.investigation.wizard.adminRequired")}
          </p>
        )}

        {/* 4. Кнопки */}
        <div className="flex justify-end gap-3">
          {onCancel && (
            <Button variant="ghost" onClick={onCancel} disabled={startInvestigation.isPending}>
              {t("performance.investigation.wizard.cancel")}
            </Button>
          )}
          <Button
            disabled={!canStart || startInvestigation.isPending}
            onClick={() => void handleStart()}
            title={!isAdmin ? t("performance.investigation.wizard.adminRequired") : undefined}
          >
            {startInvestigation.isPending
              ? t("performance.investigation.wizard.starting")
              : t("performance.investigation.wizard.start")}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
