import { ChevronDownIcon, DownloadIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import type { RecordingDetail } from "../types";
import { downloadBlob } from "./downloadBlob";
import { recordingFilename } from "./recordingFilename";
import { recordingToCsv } from "./recordingCsv";

interface RecordingExportMenuProps {
  detail: RecordingDetail;
}

/** Меню «Скачать» для выгрузки ряда сэмплов записи (MLC-071, образец `features/reports/export`).
 *  CSV — синхронно (лёгкий), Excel — `dynamic import` тяжёлой SheetJS по клику. Скрыт при пустом
 *  ряде сэмплов (нечего выгружать). */
export function RecordingExportMenu({ detail }: RecordingExportMenuProps) {
  const { t } = useTranslation();

  if (detail.samples.length === 0) {
    return null;
  }

  const handleCsv = () => {
    downloadBlob(recordingFilename(detail.recording, "csv"), recordingToCsv(detail.samples));
  };

  const handleXlsx = () => {
    void (async () => {
      try {
        const { recordingToXlsx } = await import("./recordingXlsx");
        downloadBlob(
          recordingFilename(detail.recording, "xlsx"),
          await recordingToXlsx(detail.recording, detail.samples)
        );
      } catch {
        toast.error(t("performance.recording.export.error"));
      }
    })();
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" className="gap-2">
          <DownloadIcon className="size-4" aria-hidden="true" />
          {t("performance.recording.export.menu")}
          <ChevronDownIcon className="size-4" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={handleCsv}>
          {t("performance.recording.export.csv")}
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={handleXlsx}>
          {t("performance.recording.export.xlsx")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
