import type { TFunction } from "i18next";
import { ApiError } from "@/lib/api";

interface ProblemBody {
  detail?: string;
  title?: string;
}

// MLC-046: короткая русская строка ошибки для строки прогресса пачки. Переиспользует
// санитизированный detail сервера (409 — гейт/сбой webinst/IIS; 422/400 — валидация
// версии), иначе — общий текст. Та же природа сообщений, что в одиночных диалогах.
export function describePublicationOpError(error: unknown, t: TFunction): string {
  if (error instanceof ApiError) {
    const body = (error.body as ProblemBody | null) ?? null;
    const detail = body?.detail ?? body?.title;
    if (error.status === 404) return t("publications.bulk.errors.notFound");
    if (error.status === 409) return detail ?? t("publications.bulk.errors.conflict");
    if (error.status === 422 || error.status === 400)
      return detail ?? t("publications.bulk.errors.validation");
    return detail ?? `HTTP ${error.status}`;
  }
  return t("errors.generic");
}
