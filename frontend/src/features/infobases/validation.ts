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
export const DATABASE_SERVER_MAX_LENGTH = 200;
export const DATABASE_NAME_MAX_LENGTH = 200;
export const SITE_NAME_MAX_LENGTH = 200;
export const VIRTUAL_PATH_MAX_LENGTH = 200;
export const PLATFORM_VERSION_MAX_LENGTH = 50;
export const PHYSICAL_PATH_MAX_LENGTH = 260;

export const STATUSES: InfobaseStatus[] = ["Active", "Maintenance", "Suspended"];

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
      .max(NAME_MAX_LENGTH, t("infobases.errors.nameTooLong")),
    clusterInfobaseId: z
      .string()
      .trim()
      .regex(GUID_PATTERN, t("infobases.errors.clusterIdInvalid")),
    databaseServer: z
      .string()
      .trim()
      .min(1, t("infobases.errors.databaseServerRequired"))
      .max(DATABASE_SERVER_MAX_LENGTH, t("infobases.errors.fieldTooLong")),
    databaseName: z
      .string()
      .trim()
      .min(1, t("infobases.errors.databaseNameRequired"))
      .max(DATABASE_NAME_MAX_LENGTH, t("infobases.errors.fieldTooLong")),
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
        .startsWith("/", t("publications.errors.virtualPathLeadingSlash"))
        .refine((v) => !/\s/.test(v), t("publications.errors.virtualPathNoSpaces")),
      platformVersion: z
        .string()
        .trim()
        .min(1, t("publications.errors.platformVersionRequired"))
        .regex(PLATFORM_VERSION_PATTERN, t("publications.errors.platformVersionFormat")),
      enableOData: z.boolean(),
      enableHttpServices: z.boolean(),
      vrdCustomXml: z.string().optional(),
      physicalPathOverride: z
        .string()
        .max(PHYSICAL_PATH_MAX_LENGTH, t("publications.errors.physicalPathOverrideTooLong"))
        .optional(),
    }),
  });
}

export type InfobaseFormValues = z.infer<ReturnType<typeof buildInfobaseFormSchema>>;
