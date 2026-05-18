import { useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api";

export interface ChangePasswordInput {
  currentPassword: string;
  newPassword: string;
}

export function useChangePassword() {
  return useMutation({
    mutationFn: async (input: ChangePasswordInput) =>
      api<null>("/api/v1/auth/change-password", { method: "POST", body: input }),
  });
}
