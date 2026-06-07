import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import {
  adminListResponseSchema,
  type AdminCreatedResponse,
  type AdminPasswordResetResponse,
  type CreateAdminInput,
} from "./types";

export const adminsQueryKey = ["admins"] as const;

export function useAdmins() {
  return useQuery({
    queryKey: adminsQueryKey,
    queryFn: () => api("/api/v1/admins", { schema: adminListResponseSchema }),
  });
}

export function useCreateAdmin() {
  return useInvalidatingMutation({
    mutationFn: (input: CreateAdminInput) =>
      api<AdminCreatedResponse>("/api/v1/admins", { method: "POST", body: input }),
    invalidate: adminsQueryKey,
  });
}

export function useResetAdminPassword() {
  return useInvalidatingMutation({
    mutationFn: (id: string) =>
      api<AdminPasswordResetResponse>(`/api/v1/admins/${id}/reset-password`, { method: "POST" }),
    invalidate: adminsQueryKey,
  });
}

export function useDisableAdmin() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/admins/${id}/disable`, { method: "POST" }),
    invalidate: adminsQueryKey,
  });
}

export function useEnableAdmin() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/admins/${id}/enable`, { method: "POST" }),
    invalidate: adminsQueryKey,
  });
}
