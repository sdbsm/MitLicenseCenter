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
}
