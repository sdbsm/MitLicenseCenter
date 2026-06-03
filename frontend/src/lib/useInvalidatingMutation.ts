import {
  useMutation,
  useQueryClient,
  type QueryKey,
  type UseMutationResult,
} from "@tanstack/react-query";

// MLC-031 (REF-03): единый генератор CRUD-мутаций. Сворачивает повторявшийся в каждой
// `features/*/use<X>.ts` шаблон `useMutation({ mutationFn, onSuccess: invalidateQueries })`
// в одно место политики инвалидации. Поведение хуков сохраняется 1:1.

// Список ключей для инвалидации. Принимаем либо один ключ (`["infobases"]`), либо массив
// ключей (`[infobasesQueryKey, tenantsQueryKey]`), либо функцию от переменных мутации, если
// ключ зависит от них (напр. `driftStatusQueryKey(publicationId)`).
type InvalidateKeys<TVariables> =
  | QueryKey
  | readonly QueryKey[]
  | ((variables: TVariables) => QueryKey | readonly QueryKey[]);

// Нормализует «один ключ или массив ключей» к списку ключей. QueryKey — это массив, поэтому
// массив ключей отличаем по тому, что все его элементы сами являются массивами.
function toKeyList(keys: QueryKey | readonly QueryKey[]): readonly QueryKey[] {
  if (keys.length > 0 && keys.every((part) => Array.isArray(part))) {
    return keys as readonly QueryKey[];
  }
  return [keys as QueryKey];
}

export interface InvalidatingMutationOptions<TData, TVariables> {
  mutationFn: (variables: TVariables) => Promise<TData>;
  // Ключи, которые надо инвалидировать после успешной мутации.
  invalidate: InvalidateKeys<TVariables>;
  // Доп-логика после инвалидации (тосты/навигация/доп-инвалидация). Необязательна.
  onSuccess?: (data: TData, variables: TVariables) => void;
}

export function useInvalidatingMutation<TData, TVariables, TError = unknown>(
  options: InvalidatingMutationOptions<TData, TVariables>
): UseMutationResult<TData, TError, TVariables> {
  const qc = useQueryClient();
  return useMutation<TData, TError, TVariables>({
    mutationFn: options.mutationFn,
    onSuccess: (data, variables) => {
      const keys =
        typeof options.invalidate === "function"
          ? options.invalidate(variables)
          : options.invalidate;
      for (const queryKey of toKeyList(keys)) {
        void qc.invalidateQueries({ queryKey });
      }
      options.onSuccess?.(data, variables);
    },
  });
}
