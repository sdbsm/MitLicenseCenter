import { useMemo } from "react";
import { computeVerdict, toFamilyShares } from "./attribution";
import { cpuSaturation, diskSaturation, ramSaturation } from "./thresholds";
import { useHostMetrics } from "./useHostMetrics";

/**
 * Оркестрация стартового live-экрана раздела «Быстродействие»: live-снимок host
 * (polling 5с) + производные сатурации/доли/вердикт. Презентация (гейджи,
 * атрибуция, баннер) вынесена в отдельные компоненты — паттерн `useSessionsPage`.
 */
export function usePerformancePage() {
  const { data, isLoading, isError, refetch, failureCount } = useHostMetrics();

  const derived = useMemo(() => {
    if (!data) return null;
    return {
      cpu: cpuSaturation(data.cpu),
      ram: ramSaturation(data.memory),
      disk: diskSaturation(data.disk),
      families: toFamilyShares(data.processGroups),
      verdict: computeVerdict(data),
    };
  }, [data]);

  return {
    snapshot: data,
    measuring: data?.measuring ?? false,
    derived,
    isLoading,
    isError,
    refetch,
    failureCount,
  };
}
