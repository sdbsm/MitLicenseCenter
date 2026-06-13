import { z } from "zod";
import type { InfobaseStatus } from "./types";

// MLC-022 — единый источник правил валидации Infobase/Publication на стороне фронта.
// Сюда вынесены regex версии платформы, GUID, max-длины и Zod-фабрика, раньше жившие в
// InfobaseFormDialog. Человекочитаемая проза-спека правил — docs/03_DOMAIN_MODEL.md (§2, §3);
// литералы закреплены parity-тестом (__tests__/validation.test.ts), парным к backend'у
// (InfobasesValidationTests.cs). Codegen не вводится (это MLC-025).

export const PLATFORM_VERSION_PATTERN = /^\d+\.\d+\.\d+\.\d+$/;
export const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

// Max-длины полей (совпадают с nvarchar-констрейнтами БД и backend DTO-аннотациями).
// VIRTUAL_PATH_MAX_LENGTH и PLATFORM_VERSION_MAX_LENGTH — документированный источник:
// длина platformVersion на практике связана regex'ом, длина virtualPath режется на
// уровне БД/DTO. Схему форму не меняем — поведение остаётся 1:1 с текущим.
export const NAME_MAX_LENGTH = 200;
export const DATABASE_NAME_MAX_LENGTH = 200;
export const SITE_NAME_MAX_LENGTH = 200;
export const VIRTUAL_PATH_MAX_LENGTH = 200;
export const PLATFORM_VERSION_MAX_LENGTH = 50;
export const PHYSICAL_PATH_MAX_LENGTH = 260;

export const STATUSES: InfobaseStatus[] = ["Active", "Maintenance", "Suspended"];

// MLC-118 — предикаты безопасности символов, зеркальные backend'у
// (InfobaseValidationRules: IsConnStrSafeName / IsSafeDatabaseName / IsSafeVirtualPath /
// IsSafePhysicalPath). Проза-спека правил — docs/03_DOMAIN_MODEL.md (§1.1, §3.5).
// Управляющие символы — U+0000–U+001F и U+007F (как char.IsControl на BE для ASCII-диапазона).
// eslint-disable-next-line no-control-regex
const CONTROL_CHARS_PATTERN = /[\u0000-\u001f\u007f]/;

// Infobase.Name → Ref=<name> строки соединения webinst: запрет «; = "» и control-символов.
export function isConnStrSafeName(value: string): boolean {
  const v = value.trim();
  return !CONTROL_CHARS_PATTERN.test(v) && !/[;="]/.test(v);
}

// Infobase.DatabaseName → Path.Combine + SQL-идентификатор: запрет control, «..» и
// служебных/path-метасимволов \ / : * ? " < > | ; ' [ ].
export function isSafeDatabaseName(value: string): boolean {
  const v = value.trim();
  return !CONTROL_CHARS_PATTERN.test(v) && !v.includes("..") && !/[\\/:*?"<>|;'[\]]/.test(v);
}

// Publication.VirtualPath: запрет control, обратного слеша «\» и «..».
export function isSafeVirtualPath(value: string): boolean {
  const v = value.trim();
  return !CONTROL_CHARS_PATTERN.test(v) && !v.includes("\\") && !v.includes("..");
}

// Publication.PhysicalPathOverride: запрет control, «..» и «; = "» (\ / : легитимны в абс. пути).
export function isSafePhysicalPath(value: string): boolean {
  const v = value.trim();
  return !CONTROL_CHARS_PATTERN.test(v) && !v.includes("..") && !/[;="]/.test(v);
}

export function buildInfobaseFormSchema(t: (k: string) => string) {
  return z.object({
    tenantId: z
      .string()
      .min(1, t("infobases.errors.tenantRequired"))
      .regex(GUID_PATTERN, t("infobases.errors.tenantRequired")),
    name: z
      .string()
      .trim()
      .min(1, t("infobases.errors.nameRequired"))
      .max(NAME_MAX_LENGTH, t("infobases.errors.nameTooLong"))
      // MLC-118 (BE-07/SEC-13): connstr-safe — без «; = "» и control-символов.
      .refine(isConnStrSafeName, t("infobases.errors.nameInvalidChars")),
    clusterInfobaseId: z
      .string()
      .trim()
      .regex(GUID_PATTERN, t("infobases.errors.clusterIdInvalid")),
    databaseName: z
      .string()
      .trim()
      .min(1, t("infobases.errors.databaseNameRequired"))
      .max(DATABASE_NAME_MAX_LENGTH, t("infobases.errors.fieldTooLong"))
      // MLC-118 (SEC-12/UX-11): без служебных/path-метасимволов и «..».
      .refine(isSafeDatabaseName, t("infobases.errors.databaseNameInvalidChars")),
    status: z.enum(STATUSES),
    publication: z.object({
      siteName: z
        .string()
        .trim()
        .min(1, t("publications.errors.siteNameRequired"))
        .max(SITE_NAME_MAX_LENGTH, t("infobases.errors.fieldTooLong")),
      virtualPath: z
        .string()
        .trim()
        .min(1, t("publications.errors.virtualPathRequired"))
        // MLC-118/FE-16: длина virtualPath теперь явно режется на форме.
        .max(VIRTUAL_PATH_MAX_LENGTH, t("infobases.errors.fieldTooLong"))
        .startsWith("/", t("publications.errors.virtualPathLeadingSlash"))
        .refine((v) => !/\s/.test(v), t("publications.errors.virtualPathNoSpaces"))
        // MLC-118 (SEC-11): без «\», «..» и control-символов.
        .refine(isSafeVirtualPath, t("publications.errors.virtualPathInvalidChars")),
      platformVersion: z
        .string()
        .trim()
        .min(1, t("publications.errors.platformVersionRequired"))
        // MLC-118/FE-16: длина platformVersion теперь явно режется на форме.
        .max(PLATFORM_VERSION_MAX_LENGTH, t("infobases.errors.fieldTooLong"))
        .regex(PLATFORM_VERSION_PATTERN, t("publications.errors.platformVersionFormat")),
      physicalPathOverride: z
        .string()
        .max(PHYSICAL_PATH_MAX_LENGTH, t("publications.errors.physicalPathOverrideTooLong"))
        // MLC-118 (SEC-11): без «..», «; = "» и control-символов (\ / : легитимны).
        .refine(isSafePhysicalPath, t("publications.errors.physicalPathOverrideInvalidChars"))
        .optional(),
    }),
  });
}

export type InfobaseFormValues = z.infer<ReturnType<typeof buildInfobaseFormSchema>>;
