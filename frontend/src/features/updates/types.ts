import { z } from "zod";

// MLC-176 — статус проверки обновлений (`GET /api/v1/updates/status`,
// `POST /api/v1/updates/check-now`). Контракт зафиксирован на бэкенде
// (Web/Endpoints/Updates/UpdatesContracts.cs) — поля совпадают 1:1.
//
// Nullable-поля через `.nullish()`: когда проверка недоступна (checkAvailable=false),
// API ОПУСКАЕТ latestVersion/releaseUrl/downloadUrl (известный урок проекта — иначе
// runtime-ошибка Zod на отсутствующем поле). Схема толерантна к доп.полям будущего.
export const updateStatusSchema = z.object({
  currentVersion: z.string(),
  latestVersion: z.string().nullish(),
  updateAvailable: z.boolean(),
  releaseUrl: z.string().nullish(),
  downloadUrl: z.string().nullish(),
  checkAvailable: z.boolean(),
  checkedAtUtc: z.string(),
});

export type UpdateStatus = z.infer<typeof updateStatusSchema>;
