import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

// Роли панели. Назначаются при создании учётки; смену роли у существующих учёток
// добавляет MLC-061.
export const USER_ROLES = ["Admin", "Viewer"] as const;
export type UserRole = (typeof USER_ROLES)[number];

export const userSchema = z.object({
  id: z.string(),
  userName: z.string(),
  roles: z.array(z.string()),
  // Identity-lockout = «отключена»; активна — когда lockout не действует.
  isActive: z.boolean(),
  // MLC-059 — время последнего успешного входа (ISO-UTC), null — ни разу не входил.
  // omittable: бэкенд опускает ключ при null (WhenWritingNull) — принимаем оба варианта.
  lastLoginAt: omittable(z.string()),
});

// Список учёток (`GET /api/v1/users`) — простой конверт без пагинации (учёток единицы).
export const userListResponseSchema = z.object({
  items: z.array(userSchema),
});

export type User = z.infer<typeof userSchema>;
export type UserListResponse = z.infer<typeof userListResponseSchema>;

// Тело запроса (не ответ) — рукописное, runtime-валидация не нужна.
export interface CreateUserInput {
  userName: string;
  role: UserRole;
}

// Ответы создания/сброса несут сгенерированный временный пароль — он показывается в UI
// один раз и нигде не сохраняется.
export interface UserCreatedResponse {
  id: string;
  userName: string;
  generatedPassword: string;
}

export interface UserPasswordResetResponse {
  generatedPassword: string;
}
