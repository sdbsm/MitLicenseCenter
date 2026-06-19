import { useTranslation } from "react-i18next";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import type { IisObjectState } from "./iisTypes";

const VARIANT: Record<IisObjectState, StatusBadgeVariant> = {
  Started: "success",
  Stopped: "neutral",
  Starting: "warning",
  Stopping: "warning",
  Unknown: "neutral",
};

// MLC-047: бейдж состояния пула/сайта IIS. Текст — из i18n по имени состояния.
export function IisStateBadge({ state }: { state: IisObjectState }) {
  const { t } = useTranslation();
  return <StatusBadge variant={VARIANT[state]}>{t(`publications.iis.state.${state}`)}</StatusBadge>;
}
