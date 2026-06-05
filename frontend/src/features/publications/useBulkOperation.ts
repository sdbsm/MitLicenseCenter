import { useCallback, useMemo, useRef, useState } from "react";

// MLC-046: оркестрация массовой операции над публикациями. Пачка исполняется как N
// идемпотентных одиночных вызовов существующих эндпоинтов (publish/change-platform)
// пулом с малым параллелизмом — частичный успех и прогресс собираются на клиенте.
// Бэкенд дополнительно кэпит одновременные спавны webinst (IWebinstConcurrencyGate),
// здесь же лимит держит число открытых HTTP-запросов умеренным.

export type BulkItemStatus = "pending" | "running" | "ok" | "error" | "skipped";

export interface BulkItemState {
  id: string;
  label: string;
  status: BulkItemStatus;
  error?: string;
}

export interface BulkRunItem {
  id: string;
  label: string;
}

export interface BulkSummary {
  total: number;
  ok: number;
  error: number;
  skipped: number;
  done: number;
}

export type BulkPhase = "idle" | "running" | "done";

interface UseBulkOperationOptions {
  /** Сколько операций гнать одновременно. */
  concurrency?: number;
  /** Выполнить операцию над одним элементом; бросает при ошибке. */
  runItem: (id: string) => Promise<void>;
  /** Короткая строка ошибки для строки прогресса (маппинг ApiError → текст). */
  describeError: (error: unknown) => string;
  /** Вызывается один раз по завершении пачки (инвалидация списка, очистка выделения). */
  onComplete?: (states: BulkItemState[]) => void;
}

export function useBulkOperation({
  concurrency = 3,
  runItem,
  describeError,
  onComplete,
}: UseBulkOperationOptions) {
  const [states, setStates] = useState<BulkItemState[]>([]);
  const [phase, setPhase] = useState<BulkPhase>("idle");
  const cancelledRef = useRef(false);

  const start = useCallback(
    async (items: BulkRunItem[]) => {
      cancelledRef.current = false;
      // Локальная истина прогона (не зависит от батчинга setState) — её и отдаём в onComplete.
      const result = new Map<string, BulkItemState>(
        items.map((it) => [it.id, { id: it.id, label: it.label, status: "pending" }])
      );
      const flush = () => setStates(items.map((it) => result.get(it.id)!));
      flush();
      setPhase("running");

      let cursor = 0;
      const worker = async () => {
        // JS однопоточен — инкремент cursor атомарен между await'ами.
        for (;;) {
          if (cancelledRef.current) return;
          const index = cursor++;
          if (index >= items.length) return;
          const item = items[index];
          result.set(item.id, { ...result.get(item.id)!, status: "running" });
          flush();
          try {
            await runItem(item.id);
            result.set(item.id, { ...result.get(item.id)!, status: "ok" });
          } catch (error) {
            result.set(item.id, {
              ...result.get(item.id)!,
              status: "error",
              error: describeError(error),
            });
          }
          flush();
        }
      };

      const poolSize = Math.max(1, Math.min(concurrency, items.length));
      await Promise.all(Array.from({ length: poolSize }, () => worker()));

      // Отмена прекращает запуск новых; уже запущенные доезжают, остаток — «пропущено».
      if (cancelledRef.current) {
        for (const [id, s] of result)
          if (s.status === "pending") result.set(id, { ...s, status: "skipped" });
      }
      flush();
      setPhase("done");
      onComplete?.(Array.from(result.values()));
    },
    [concurrency, runItem, describeError, onComplete]
  );

  const cancel = useCallback(() => {
    cancelledRef.current = true;
  }, []);

  const reset = useCallback(() => {
    cancelledRef.current = false;
    setStates([]);
    setPhase("idle");
  }, []);

  const summary = useMemo<BulkSummary>(() => {
    const ok = states.filter((s) => s.status === "ok").length;
    const error = states.filter((s) => s.status === "error").length;
    const skipped = states.filter((s) => s.status === "skipped").length;
    return { total: states.length, ok, error, skipped, done: ok + error };
  }, [states]);

  return { states, phase, summary, start, cancel, reset };
}
