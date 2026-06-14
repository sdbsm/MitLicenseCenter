import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { reportsQueryKey } from "@/features/reports/reportsQueryKeys";
import { tenantListResponseSchema, type Tenant, type TenantInput } from "./types";

export const tenantsQueryKey = ["tenants"] as const;

export const TENANTS_PAGE_SIZE = 25;
// Выпадающие списки клиентов (фильтры, формы) подгружают «все» одной страницей.
// Клиентов на порядок меньше, чем инфобаз, поэтому это приемлемо (MLC-015); если их
// станет больше предела — список станет искомым/пагинированным отдельной задачей.
const ALL_TENANTS_PAGE_SIZE = 200;

// Серверная пагинация (MLC-015). queryKey включает page/pageSize, но префикс остаётся
// `["tenants"]`, поэтому мутации инвалидируют все страницы разом.
// search (UX-05, MLC-130) — подстрочный поиск по имени клиента на сервере (plain Contains→LIKE);
// пустой/undefined не добавляется в query. Включён в queryKey, чтобы кэш различал выборки.
export function useTenants(page = 1, pageSize = TENANTS_PAGE_SIZE, search?: string) {
  const term = search?.trim() ?? "";
  return useQuery({
    queryKey: [...tenantsQueryKey, { page, pageSize, search: term }],
    queryFn: () => {
      const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (term) qs.set("search", term);
      return api(`/api/v1/tenants?${qs.toString()}`, {
        schema: tenantListResponseSchema,
      });
    },
    // Не моргаем скелетоном при смене страницы — показываем предыдущие данные.
    placeholderData: (prev) => prev,
  });
}

// Полный список клиентов для выпадающих списков и карт «id → имя».
export function useAllTenants() {
  return useTenants(1, ALL_TENANTS_PAGE_SIZE);
}

export function useCreateTenant() {
  return useInvalidatingMutation({
    mutationFn: (input: TenantInput) =>
      api<Tenant>("/api/v1/tenants", { method: "POST", body: input }),
    invalidate: tenantsQueryKey,
  });
}

export function useUpdateTenant() {
  return useInvalidatingMutation({
    mutationFn: ({ id, input }: { id: string; input: TenantInput }) =>
      api<Tenant>(`/api/v1/tenants/${id}`, { method: "PUT", body: input }),
    // FE-03: при смене лимита инвалидируем и отчёты — поле Limit в отчётах
    // записывается бэкендом на момент снапшота, поэтому новые снапшоты сразу
    // отразят актуальный лимит; инвалидация обеспечивает когерентность кэша.
    invalidate: [tenantsQueryKey, reportsQueryKey],
  });
}

export function useDeleteTenant() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/tenants/${id}`, { method: "DELETE" }),
    invalidate: tenantsQueryKey,
  });
}
