import { z } from "zod";
import { omittable } from "@/lib/apiSchema";

/**
 * Zod-схема дескриптора настройки (MLC-132, FE-09).
 * Backend опускает null-поля (JsonIgnoreCondition.WhenWritingNull, ADR-32):
 * value=null (секрет или незаданная настройка) → ключ отсутствует → omittable().
 * description=null → ключ отсутствует → omittable().
 * Зеркало SettingDescriptorResponse (SettingsContracts.cs).
 */
export const settingDescriptorSchema = z.object({
  key: z.string(),
  isSecret: z.boolean(),
  isSet: z.boolean(),
  value: omittable(z.string()),
  description: omittable(z.string()),
  updatedAt: z.string(),
  updatedBy: z.string(),
});

export const settingsListSchema = z.array(settingDescriptorSchema);

export type SettingDescriptor = z.infer<typeof settingDescriptorSchema>;
