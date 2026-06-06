import { BuildingIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
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
import { LicenseUsageChart } from "./LicenseUsageChart";
import { ReportsEmptyState } from "./ReportsEmptyState";
import { ReportsStats } from "./ReportsStats";
import type { LicenseUsageSeriesResponse } from "./types";

const NO_TENANT = "__no_tenant__";

interface TenantOption {
  id: string;
  name: string;
}

interface ReportsDetailProps {
  tenants: TenantOption[];
  selectedTenantId: string | null;
  data: LicenseUsageSeriesResponse | undefined;
  isLoading: boolean;
  onSelectTenant: (tenantId: string | null) => void;
}

/** Блок детализации по клиенту: выбор тенанта → ряд тенанта тем же графиком.
 *  Без выбора — плейсхолдер; пустой ряд выбранного клиента — empty-state. */
export function ReportsDetail({
  tenants,
  selectedTenantId,
  data,
  isLoading,
  onSelectTenant,
}: ReportsDetailProps) {
  const { t } = useTranslation();
  const hasTenant = selectedTenantId !== null;
  const isEmpty = !data || data.buckets.length === 0;

  return (
    <Card>
      <CardHeader className="gap-3">
        <CardTitle>{t("reports.detail.title")}</CardTitle>
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
      <CardContent className="space-y-3">
        {!hasTenant ? (
          <div className="flex h-[320px] flex-col items-center justify-center gap-3 text-center">
            <BuildingIcon className="text-muted-foreground size-8" />
            <p className="text-muted-foreground text-sm">{t("reports.detail.placeholder")}</p>
          </div>
        ) : data && !isEmpty ? (
          <>
            <ReportsStats data={data} />
            <LicenseUsageChart buckets={data.buckets} isLoading={isLoading} />
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
