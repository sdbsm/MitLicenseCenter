import { BuildingIcon, ExternalLinkIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatBytes } from "@/lib/formatBytes";
import { DatabaseSizeChart } from "./DatabaseSizeChart";
import { ReportsEmptyState } from "./ReportsEmptyState";
import type { DatabaseSizeTenantSeriesResponse } from "./types";

const NO_TENANT = "__no_tenant__";

interface TenantOption {
  id: string;
  name: string;
}

interface DatabaseSizeDetailProps {
  tenants: TenantOption[];
  selectedTenantId: string | null;
  selectedTenantName: string | null;
  data: DatabaseSizeTenantSeriesResponse | undefined;
  isLoading: boolean;
  onSelectTenant: (tenantId: string | null) => void;
}

/** Drill-down размера баз по клиенту: выбор тенанта → ряд клиента во времени тем же
 *  графиком + таблица его баз на последний снимок. Без выбора — плейсхолдер; пустой ряд
 *  выбранного клиента — empty-state. Экспорт — 185g, здесь нет. */
export function DatabaseSizeDetail({
  tenants,
  selectedTenantId,
  selectedTenantName,
  data,
  isLoading,
  onSelectTenant,
}: DatabaseSizeDetailProps) {
  const { t } = useTranslation();
  const hasTenant = selectedTenantId !== null;
  const isEmpty = !data || data.points.length === 0;

  return (
    <Card>
      <CardHeader className="gap-3">
        <div className="flex items-center gap-3">
          <CardTitle>{t("reports.size.detail.title")}</CardTitle>
          {selectedTenantId && (
            <Link
              to={`/tenants/${selectedTenantId}`}
              className="text-primary flex items-center gap-1 text-xs underline"
            >
              <ExternalLinkIcon className="size-3" />
              {t("reports.detail.tenantLink")}
            </Link>
          )}
        </div>
        <div className="grid max-w-sm gap-1.5">
          <Label className="text-xs font-medium">{t("reports.detail.tenant")}</Label>
          <Select
            value={selectedTenantId ?? NO_TENANT}
            onValueChange={(value) => onSelectTenant(value === NO_TENANT ? null : value)}
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NO_TENANT}>{t("reports.detail.noTenant")}</SelectItem>
              {tenants.map((tenant) => (
                <SelectItem key={tenant.id} value={tenant.id}>
                  {tenant.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {!hasTenant ? (
          <div className="flex h-[320px] flex-col items-center justify-center gap-3 text-center">
            <BuildingIcon className="text-muted-foreground size-8" />
            <p className="text-muted-foreground text-sm">{t("reports.size.detail.placeholder")}</p>
          </div>
        ) : data && !isEmpty ? (
          <>
            {selectedTenantName && (
              <p className="text-muted-foreground text-sm">{selectedTenantName}</p>
            )}
            <DatabaseSizeChart points={data.points} isLoading={isLoading} />

            {data.databases.length > 0 && (
              <div className="space-y-2">
                <h3 className="text-sm font-medium">{t("reports.size.databases.title")}</h3>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t("reports.size.columns.database")}</TableHead>
                      <TableHead className="text-right">
                        {t("reports.size.columns.total")}
                      </TableHead>
                      <TableHead className="text-right">{t("reports.size.columns.data")}</TableHead>
                      <TableHead className="text-right">{t("reports.size.columns.log")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.databases.map((db) => (
                      <TableRow key={db.databaseName}>
                        <TableCell className="font-medium">{db.databaseName}</TableCell>
                        <TableCell className="text-right font-medium tabular-nums">
                          {formatBytes(db.totalBytes)}
                        </TableCell>
                        <TableCell className="text-muted-foreground text-right tabular-nums">
                          {formatBytes(db.dataBytes)}
                        </TableCell>
                        <TableCell className="text-muted-foreground text-right tabular-nums">
                          {formatBytes(db.logBytes)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </>
        ) : isLoading && !data ? (
          <Skeleton className="h-[320px] w-full" />
        ) : (
          <ReportsEmptyState />
        )}
      </CardContent>
    </Card>
  );
}
