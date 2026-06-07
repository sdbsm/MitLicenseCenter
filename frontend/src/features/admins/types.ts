import { z } from "zod";

// Роли панели. Назначаются при создании учётки; смену роли у существующих учёток
// раздел не поддерживает (вне объёма MLC-058).
export const ADMIN_ROLES = ["Admin", "Viewer"] as const;
export type AdminRole = (typeof ADMIN_ROLES)[number];

export const adminSchema = z.object({
  id: z.string(),
  userName: z.string(),
  roles: z.array(z.string()),
  // Identity-lockout = «отключена»; активна — когда lockout не действует.
  isActive: z.boolean(),
});

// Список учёток (`GET /api/v1/admins`) — простой конверт без пагинации (учёток единицы).
export const adminListResponseSchema = z.object({
  items: z.array(adminSchema),
});

export type Admin = z.infer<typeof adminSchema>;
export type AdminListResponse = z.infer<typeof adminListResponseSchema>;

// Тело запроса (не ответ) — рукописное, runtime-валидация не нужна.
export interface CreateAdminInput {
  userName: string;
  role: AdminRole;
}

// Ответы создания/сброса несут сгенерированный временный пароль — он показывается в UI
// один раз и нигде не сохраняется.
export interface AdminCreatedResponse {
  id: string;
  userName: string;
  generatedPassword: string;
}

export interface AdminPasswordResetResponse {
  generatedPassword: string;
}
