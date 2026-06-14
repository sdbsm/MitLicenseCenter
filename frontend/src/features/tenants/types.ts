import { z } from "zod";
import { omittable, pagedResponseSchema } from "@/lib/apiSchema";

export const tenantSchema = z.object({
  id: z.string(),
  name: z.string(),
  maxConcurrentLicenses: z.number(),
  isActive: z.boolean(),
  createdAt: z.string(),
  updatedAt: omittable(z.string()),
  infobaseCount: z.number(),
  // MLC-136 (R12c) — токен оптимистической блокировки (SQL Server rowversion → base64).
  // Форма редактирования шлёт его обратно; конкурентный апдейт с устаревшим токеном → 409
  // (TENANT_CONCURRENCY_CONFLICT). API опускает null-поля (omit-null, [[api-omits-null-fields]]):
  // под InMemory-тестами/до первой записи токена нет — omittable принимает отсутствие/null.
  rowVersion: omittable(z.string()),
});

/**
 * Список клиентов (`GET /api/v1/tenants`). Критичная граница (MLC-016):
 * пагинированный конверт валидируется runtime-схемой (расхождение `total`/формы
 * элемента ломает пагинацию). Типы выводятся из схем (`z.infer`).
 */
export const tenantListResponseSchema = pagedResponseSchema(tenantSchema);

export type Tenant = z.infer<typeof tenantSchema>;
export type TenantListResponse = z.infer<typeof tenantListResponseSchema>;

// Тело запроса (не ответ) — runtime-валидация не нужна, оставляем рукописным.
export interface TenantInput {
  name: string;
  maxConcurrentLicenses: number;
  isActive: boolean;
  // MLC-136 (R12c) — прочитанный клиентом rowversion, отправляемый обратно при
  // редактировании (опционально: при создании отсутствует). Сервер сверяет его как
  // ожидаемую версию; рассинхрон → 409 TENANT_CONCURRENCY_CONFLICT.
  rowVersion?: string;
}
