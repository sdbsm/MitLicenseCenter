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
import { downloadBlob } from "./downloadBlob";
import { sizeExportFilename, type SizeExportData } from "./sizeExport";
import { toSizeCsv } from "./toSizeCsv";

interface SizeExportMenuProps {
  data: SizeExportData | undefined;
}

/** Меню «Скачать» для отчёта размера баз (MLC-185g). Зеркало ExportMenu лицензий: один
 *  компонент на оба разреза — сводку по хосту и детализацию по клиенту (разрез несёт сам
 *  `data.scope`). Скрыт при пустом ряде. Тяжёлые сериалайзеры (XLSX, интерактивный HTML на
 *  Chart.js, PDF на jsPDF) грузятся `dynamic import` по клику — в основной бандл не попадают.
 *  i18n-ключи экспорта переиспользуются у лицензий (`reports.export.*`). */
export function SizeExportMenu({ data }: SizeExportMenuProps) {
  const { t } = useTranslation();

  if (!data || data.points.length === 0) {
    return null;
  }

  const handleCsv = () => {
    downloadBlob(sizeExportFilename(data, "csv"), toSizeCsv(data));
  };

  const handleXlsx = () => {
    void (async () => {
      try {
        const { toSizeXlsx } = await import("./toSizeXlsx");
        downloadBlob(sizeExportFilename(data, "xlsx"), await toSizeXlsx(data));
      } catch {
        toast.error(t("reports.export.error"));
      }
    })();
  };

  const handleHtml = () => {
    void (async () => {
      try {
        const { toSizeHtml } = await import("./toSizeHtml");
        downloadBlob(sizeExportFilename(data, "html"), toSizeHtml(data));
      } catch {
        toast.error(t("reports.export.error"));
      }
    })();
  };

  const handlePdf = () => {
    void (async () => {
      try {
        const { toSizePdf } = await import("./toSizePdf");
        downloadBlob(sizeExportFilename(data, "pdf"), await toSizePdf(data));
      } catch {
        toast.error(t("reports.export.error"));
      }
    })();
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" className="gap-2">
          <DownloadIcon className="size-4" aria-hidden="true" />
          {t("reports.export.menu")}
          <ChevronDownIcon className="size-4" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={handleCsv}>{t("reports.export.csv")}</DropdownMenuItem>
        <DropdownMenuItem onSelect={handleXlsx}>{t("reports.export.xlsx")}</DropdownMenuItem>
        <DropdownMenuItem onSelect={handleHtml}>{t("reports.export.html")}</DropdownMenuItem>
        <DropdownMenuItem onSelect={handlePdf}>{t("reports.export.pdf")}</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
