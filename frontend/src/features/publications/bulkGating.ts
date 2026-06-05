import type { PublicationListItem } from "./types";

// MLC-046: подмножество выбранных публикаций, которые webinst перезатрёт «вслепую» —
// созданные не панелью (Source ≠ Webinst) и уже опубликованные. Для них bulk-диалог
// показывает единое предупреждение со списком (та же природа, что одиночный гейт
// PUBLISH_CONFIRM_REQUIRED). confirm=true снимает гейт сервера по каждой такой записи.
export function publicationsNeedingOverwriteConfirm(
  items: PublicationListItem[]
): PublicationListItem[] {
  return items.filter((p) => p.source !== "Webinst" && p.lastCheckStatus === "Published");
}
