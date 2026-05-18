import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Tenant, TenantInput, TenantListResponse } from "./types";

export const tenantsQueryKey = ["tenants"] as const;

export function useTenants() {
  return useQuery({
    queryKey: tenantsQueryKey,
    queryFn: () => api<TenantListResponse>("/api/v1/tenants?page=1&pageSize=200"),
  });
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
    mutationFn: (id: string) =>
      api<null>(`/api/v1/tenants/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: tenantsQueryKey });
    },
  });
}
