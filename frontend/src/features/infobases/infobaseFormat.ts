import type { StatusBadgeVariant } from "@/components/ui/StatusBadge";
import type { InfobaseStatus } from "./types";

// Статус инфобазы → семантический вариант StatusBadge (docs/06_UI_GUIDE.md §1).
// Active — норма (success), Maintenance — пороговое (warning), Suspended — danger.
export function statusBadgeVariant(status: InfobaseStatus): StatusBadgeVariant {
  switch (status) {
    case "Active":
      return "success";
    case "Maintenance":
      return "warning";
    case "Suspended":
      return "danger";
  }
}
