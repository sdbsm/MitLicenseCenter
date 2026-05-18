import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@/lib/api";

export interface CurrentUser {
  userName: string;
  roles: string[];
}

const ME_KEY = ["auth", "me"] as const;

async function fetchMe(): Promise<CurrentUser | null> {
  try {
    return await api<CurrentUser>("/api/v1/auth/me");
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }
    throw error;
  }
}

export function useMe() {
  return useQuery({
    queryKey: ME_KEY,
    queryFn: fetchMe,
    staleTime: 5 * 60_000,
    refetchInterval: false,
  });
}

export function useLogin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { userName: string; password: string }) =>
      api<CurrentUser>("/api/v1/auth/login", { method: "POST", body: input }),
    onSuccess: (user) => {
      qc.setQueryData(ME_KEY, user);
    },
  });
}

export function useLogout() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => api<null>("/api/v1/auth/logout", { method: "POST" }),
    onSuccess: () => {
      qc.setQueryData(ME_KEY, null);
      qc.clear();
    },
  });
}
