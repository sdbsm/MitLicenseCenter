import { useCallback, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { computeVerdict, toFamilyShares } from "./attribution";
import { cpuSaturation, diskSaturation, ramSaturation } from "./thresholds";
import { hostMetricsQueryKey, useHostMetrics } from "./useHostMetrics";
import { oneCLoadQueryKey } from "./useOneCLoad";
import { sqlPerformanceQueryKey } from "./useSqlPerformance";

/**
 * Оркестрация стартового live-экрана раздела «Быстродействие»: live-снимок host
 * (polling 5с) + производные сатурации/доли/вердикт. Презентация (гейджи,
 * атрибуция, баннер) вынесена в отдельные компоненты — паттерн `useSessionsPage`.
 *
 * MLC-156: страница владеет единым `isPaused` (общий контрол LiveControls для всех
 * live-секций: host + 1С + SQL). «Обновить сейчас» = refetch всех трёх live-источников
 * (perf уже live на каждый poll — отдельный бэкенд-форс не нужен, в отличие от /sessions).
 */
export function usePerformancePage() {
  const queryClient = useQueryClient();

  const [isPaused, setIsPaused] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const togglePause = useCallback(() => setIsPaused((p) => !p), []);

  const { data, isLoading, isError, refetch, failureCount } = useHostMetrics(isPaused);

  // Refetch ровно трёх live-источников раздела (host/1С/SQL). Точечно по их ключам —
  // recordings (персистируемый список, не live) под рефетч намеренно не попадает.
  const refreshNow = useCallback(async () => {
    setIsRefreshing(true);
    try {
      await Promise.all([
        queryClient.refetchQueries({ queryKey: hostMetricsQueryKey }),
        queryClient.refetchQueries({ queryKey: oneCLoadQueryKey }),
        queryClient.refetchQueries({ queryKey: sqlPerformanceQueryKey }),
      ]);
    } finally {
      setIsRefreshing(false);
    }
  }, [queryClient]);

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
    isPaused,
    togglePause,
    refreshNow,
    isRefreshing,
  };
}
