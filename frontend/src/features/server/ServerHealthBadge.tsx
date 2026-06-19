import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";

// Общий индикатор здоровья узла (светофор по overall, MLC-214). Маппинг известных
// значений; незнакомое будущее значение → нейтральный (граница не падает).
// Статус — только через StatusBadge (инвариант ADR-46).
const OVERALL_VARIANT: Record<string, StatusBadgeVariant> = {
  Healthy: "success",
  Degraded: "warning",
  Down: "danger",
  Unknown: "neutral",
};

export function ServerHealthBadge({ overall }: { overall: string }) {
  const { t } = useTranslation();
  return (
    <StatusBadge variant={OVERALL_VARIANT[overall] ?? "neutral"}>
      {t(`server.health.${overall}`, { defaultValue: overall })}
    </StatusBadge>
  );
}
