import { ApiError } from "@/lib/api";
import { matchConflictCode } from "@/lib/apiErrors";

// MLC-023 — чистый перевод ошибки API в дескриптор ошибки поля формы. i18n, form.setError
// и toast остаются в useInfobaseForm; здесь — только классификация (тестируется отдельно
// от рендера, см. __tests__/mapConflictToField.test.ts). Возврат null = известного маппинга
// нет, вызывающая сторона делает прежний fallback (ApiError 400 → message, иначе generic).
// MLC-033 — 409-ветка свёрнута в обобщённый matchConflictCode (lib/apiErrors); 404-кейс
// (не 409) остаётся отдельной веткой сверху.

export interface ConflictFieldError {
  field: "name" | "clusterInfobaseId" | "tenantId";
  messageKey: string;
  // Поле живёт в свёрнутом блоке «Дополнительно» — его надо раскрыть, иначе ошибка не видна.
  openAdvanced?: boolean;
}

export function mapConflictToField(error: unknown): ConflictFieldError | null {
  const mapped = matchConflictCode<ConflictFieldError>(error, {
    NAME_DUPLICATE_IN_TENANT: {
      field: "name",
      messageKey: "infobases.errors.nameDuplicate",
      openAdvanced: true,
    },
    INFOBASE_ALREADY_ASSIGNED: {
      field: "clusterInfobaseId",
      messageKey: "infobases.errors.clusterAlreadyAssigned",
    },
  });
  if (mapped) return mapped;

  if (error instanceof ApiError && error.status === 404) {
    return { field: "tenantId", messageKey: "infobases.errors.tenantNotFound" };
  }

  return null;
}
