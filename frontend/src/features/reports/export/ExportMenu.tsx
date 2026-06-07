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
import type { LicenseUsageSeriesResponse } from "../types";
import { downloadBlob } from "./downloadBlob";
import { exportFilename, type ExportScope } from "./exportFilename";
import { toCsv } from "./toCsv";

interface ExportMenuProps {
  data: LicenseUsageSeriesResponse | undefined;
  scope: ExportScope;
}

/** Меню «Скачать» для выгрузки видимого ряда (MLC-051). Один компонент на оба
 *  разреза — сводку (`scope="all"`) и детализацию (`scope={{ tenantName }}`),
 *  они выгружаются по отдельности. Скрыт при пустом ряде. Тяжёлые сериалайзеры
 *  (XLSX, интерактивный HTML на Chart.js, PDF на jsPDF) грузятся `dynamic import`
 *  по клику — в основной бандл не попадают. */
export function ExportMenu({ data, scope }: ExportMenuProps) {
  const { t } = useTranslation();

  if (!data || data.buckets.length === 0) {
    return null;
  }

  const handleCsv = () => {
    downloadBlob(exportFilename(scope, data, "csv"), toCsv(data));
  };

  const handleXlsx = () => {
    void (async () => {
      try {
        const { toXlsx } = await import("./toXlsx");
        downloadBlob(exportFilename(scope, data, "xlsx"), await toXlsx(data, scope));
      } catch {
        toast.error(t("reports.export.error"));
      }
    })();
  };

  const handleHtml = () => {
    void (async () => {
      try {
        const { toHtml } = await import("./toHtml");
        downloadBlob(exportFilename(scope, data, "html"), toHtml(data, scope));
      } catch {
        toast.error(t("reports.export.error"));
      }
    })();
  };

  const handlePdf = () => {
    void (async () => {
      try {
        const { toPdf } = await import("./toPdf");
        downloadBlob(exportFilename(scope, data, "pdf"), await toPdf(data, scope));
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
