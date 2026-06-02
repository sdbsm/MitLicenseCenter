import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Tenant, TenantInput, TenantListResponse } from "./types";

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
    queryFn: () => api<TenantListResponse>(`/api/v1/tenants?page=${page}&pageSize=${pageSize}`),
    // Не моргаем скелетоном при смене страницы — показываем предыдущие данные.
    placeholderData: (prev) => prev,
  });
}

// Полный список клиентов для выпадающих списков и карт «id → имя».
export function useAllTenants() {
  return useTenants(1, ALL_TENANTS_PAGE_SIZE);
}

export function useCreateTenant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: TenantInput) =>
      api<Tenant>("/api/v1/tenants", { method: "POST", body: input }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: tenantsQueryKey });
    },
  });
}

export function useUpdateTenant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: TenantInput }) =>
      api<Tenant>(`/api/v1/tenants/${id}`, { method: "PUT", body: input }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: tenantsQueryKey });
    },
  });
}

export function useDeleteTenant() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/tenants/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: tenantsQueryKey });
    },
  });
}
