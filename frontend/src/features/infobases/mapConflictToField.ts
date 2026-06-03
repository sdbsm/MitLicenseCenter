import { ApiError, readConflictBody } from "@/lib/api";

// MLC-023 — чистый перевод ошибки API в дескриптор ошибки поля формы. i18n, form.setError
// и toast остаются в useInfobaseForm; здесь — только классификация (тестируется отдельно
// от рендера, см. __tests__/mapConflictToField.test.ts). Возврат null = известного маппинга
// нет, вызывающая сторона делает прежний fallback (ApiError 400 → message, иначе generic).

export interface ConflictFieldError {
  field: "name" | "clusterInfobaseId" | "tenantId";
  messageKey: string;
  // Поле живёт в свёрнутом блоке «Дополнительно» — его надо раскрыть, иначе ошибка не видна.
  openAdvanced?: boolean;
}

export function mapConflictToField(error: unknown): ConflictFieldError | null {
  if (!(error instanceof ApiError)) return null;

  if (error.status === 409) {
    const body = readConflictBody(error);
    if (body?.code === "NAME_DUPLICATE_IN_TENANT") {
      return { field: "name", messageKey: "infobases.errors.nameDuplicate", openAdvanced: true };
    }
    if (body?.code === "INFOBASE_ALREADY_ASSIGNED") {
      return { field: "clusterInfobaseId", messageKey: "infobases.errors.clusterAlreadyAssigned" };
    }
  }

  if (error.status === 404) {
    return { field: "tenantId", messageKey: "infobases.errors.tenantNotFound" };
  }

  return null;
}
