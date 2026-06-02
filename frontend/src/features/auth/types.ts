import { z } from "zod";

/**
 * Текущий пользователь (`GET /api/v1/auth/me`, `POST /api/v1/auth/login`).
 * Критичная граница (MLC-016): `roles` управляет ролевым гейтингом
 * `ProtectedRoute`, поэтому ответ проходит runtime-валидацию этой схемой —
 * тихое расхождение контракта здесь означало бы ошибку авторизации.
 */
export const currentUserSchema = z.object({
  userName: z.string(),
  roles: z.array(z.string()),
});

export type CurrentUser = z.infer<typeof currentUserSchema>;
