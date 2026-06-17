import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useInvalidatingMutation } from "@/lib/useInvalidatingMutation";
import {
  userListResponseSchema,
  type UserCreatedResponse,
  type UserPasswordResetResponse,
  type CreateUserInput,
  type ChangeUserRoleInput,
} from "./types";

export const usersQueryKey = ["users"] as const;

export function useUsers() {
  return useQuery({
    queryKey: usersQueryKey,
    queryFn: () => api("/api/v1/users", { schema: userListResponseSchema }),
  });
}

export function useCreateUser() {
  return useInvalidatingMutation({
    mutationFn: (input: CreateUserInput) =>
      api<UserCreatedResponse>("/api/v1/users", { method: "POST", body: input }),
    invalidate: usersQueryKey,
  });
}

export function useResetUserPassword() {
  return useInvalidatingMutation({
    mutationFn: (id: string) =>
      api<UserPasswordResetResponse>(`/api/v1/users/${id}/reset-password`, { method: "POST" }),
    invalidate: usersQueryKey,
  });
}

export function useDisableUser() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/users/${id}/disable`, { method: "POST" }),
    invalidate: usersQueryKey,
  });
}

export function useEnableUser() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/users/${id}/enable`, { method: "POST" }),
    invalidate: usersQueryKey,
  });
}

// MLC-180 — жёсткое удаление учётки (DELETE /api/v1/users/{id}). Ответ 204 (без тела).
export function useDeleteUser() {
  return useInvalidatingMutation({
    mutationFn: (id: string) => api<null>(`/api/v1/users/${id}`, { method: "DELETE" }),
    invalidate: usersQueryKey,
  });
}

export function useChangeUserRole() {
  return useInvalidatingMutation({
    mutationFn: ({ id, role }: ChangeUserRoleInput) =>
      api<null>(`/api/v1/users/${id}/role`, { method: "POST", body: { role } }),
    invalidate: usersQueryKey,
  });
}
