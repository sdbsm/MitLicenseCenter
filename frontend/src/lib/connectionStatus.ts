import { useSyncExternalStore } from "react";

// MLC-121 (UX-03/FE-05) — глобальный индикатор состояния соединения. Минимальный
// module-level store без внешних зависимостей: первичный сигнал «нет связи» —
// фактический ApiNetworkError из любого запроса (поднимается через QueryCache/
// MutationCache.onError в queryClient), снимается при следующем успешном запросе.
// Подписка из React — через useSyncExternalStore (без контекста и провайдера).

let offline = false;
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) listener();
}

/** Помечает соединение как потерянное (вызывается из onError при ApiNetworkError). */
export function markOffline(): void {
  if (offline) return;
  offline = true;
  emit();
}

/** Снимает признак потери связи (вызывается из onSuccess любого запроса). */
export function markOnline(): void {
  if (!offline) return;
  offline = false;
  emit();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function getSnapshot(): boolean {
  return offline;
}

/** React-хук: `true`, когда последний сигнал — сетевой сбой без последующего успеха. */
export function useIsOffline(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
