import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import { tenantListResponseSchema, type Tenant, type TenantInput } from "./types";

export const tenantsQueryKey = ["tenants"] as const;

export const TENANTS_PAGE_SIZE = 25;
// Выпадающие списки клиентов (фильтры, формы) подгружают «все» одной страницей.
// Клиентов на порядок меньше, чем инфобаз, поэтому это приемлемо (MLC-015); если их
// станет больше предела — список станет искомым/пагинированным отдельной задачей.
const ALL_TENANTS_PAGE_SIZE = 200;

// Серверная пагинация (MLC-015). queryKey включает page/pageSize, но префикс остаётся
// `["tenants"]`, поэтому мутации инвалидируют все страницы разом.
export function useTenants(page = 1, pageSize = TENANTS_PAGE_SIZE) {
  return useQuery({
    queryKey: [...tenantsQueryKey, { page, pageSize }],
    queryFn: () =>
      api(`/api/v1/tenants?page=${page}&pageSize=${pageSize}`, {
        schema: tenantListResponseSchema,
      }),
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
    invalidate: tenantsQueryKey,
  });
}

export function useDeleteTenant() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/tenants/${id}`, { method: "DELETE" }),
    invalidate: tenantsQueryKey,
  });
}
