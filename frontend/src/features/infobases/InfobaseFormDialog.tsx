import { useTranslation } from "react-i18next";
import { ChevronDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { DiscoveryField } from "@/features/discovery/DiscoveryField";
import type { Tenant } from "@/features/tenants/types";
import type { InfobaseListItem } from "./types";
import { useInfobaseForm } from "./useInfobaseForm";
import { PublicationFieldset } from "./PublicationFieldset";

interface InfobaseFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  infobase?: InfobaseListItem | null;
  tenants: Tenant[];
  defaultTenantId?: string;
}

// MLC-023 — тонкий вью главной формы инфобазы. Вся логика (схема, prefill, touched,
// discovery, submit, маппинг 409) живёт в useInfobaseForm; блок «Дополнительно» — в
// PublicationFieldset. Здесь только разметка диалога и видимые поля.
export function InfobaseFormDialog({
  open,
  onOpenChange,
  infobase,
  tenants,
  defaultTenantId,
}: InfobaseFormDialogProps) {
  const { t } = useTranslation();
  const {
    form,
    isEdit,
    pending,
    advancedOpen,
    setAdvancedOpen,
    onSubmit,
    infobaseOptions,
    infobasesState,
    refetchInfobases,
    databaseOptions,
    databasesState,
    refetchDatabases,
    siteOptions,
    sitesState,
    refetchSites,
    platformVersionOptions,
    platformVersionsState,
    refetchPlatformVersions,
    watchedDatabaseServer,
    computedDefaultPath,
    handleClusterChange,
    handleDatabaseNameChange,
    markNameTouched,
    markVirtualPathTouched,
    markPhysicalPathTouched,
  } = useInfobaseForm({ open, onOpenChange, infobase, tenants, defaultTenantId });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {isEdit ? t("infobases.form.editTitle") : t("infobases.form.createTitle")}
          </DialogTitle>
          <DialogDescription>{t("infobases.form.subtitle")}</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={onSubmit} noValidate className="grid gap-4">
            <FormField
              control={form.control}
              name="tenantId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("infobases.fields.tenant")}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange} disabled={isEdit}>
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue placeholder={t("infobases.form.tenantPlaceholder")} />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {tenants.map((tenant) => (
                        <SelectItem key={tenant.id} value={tenant.id}>
                          {tenant.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {isEdit && (
                    <FormDescription>{t("infobases.form.tenantLockedHint")}</FormDescription>
                  )}
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="clusterInfobaseId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("infobases.form.clusterInfobaseLabel")}</FormLabel>
                  <FormControl>
                    <DiscoveryField
                      value={field.value}
                      onChange={(v) => handleClusterChange(v, field.onChange)}
                      options={infobaseOptions}
                      available={infobasesState.available}
                      loading={infobasesState.loading}
                      error={infobasesState.error}
                      onRefresh={refetchInfobases}
                      manualPlaceholder="00000000-0000-0000-0000-000000000000"
                      inputClassName="font-mono text-xs"
                    />
                  </FormControl>
                  <FormDescription>{t("infobases.form.clusterInfobaseHint")}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="databaseName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("infobases.form.databaseNameLabel")}</FormLabel>
                  <FormControl>
                    <DiscoveryField
                      value={field.value}
                      onChange={(v) => handleDatabaseNameChange(v, field.onChange)}
                      options={databaseOptions}
                      available={databasesState.available}
                      loading={databasesState.loading}
                      error={databasesState.error}
                      onRefresh={refetchDatabases}
                      manualPlaceholder={t("infobases.form.databaseNamePlaceholder")}
                      disabledHint={
                        (watchedDatabaseServer ?? "").trim()
                          ? null
                          : t("discovery.databaseServerFirst")
                      }
                    />
                  </FormControl>
                  <FormDescription>{t("infobases.form.databaseNameSubsystemHint")}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <Separator />

            <button
              type="button"
              onClick={() => setAdvancedOpen((o) => !o)}
              aria-expanded={advancedOpen}
              className="flex items-center gap-2 text-sm font-medium"
            >
              <ChevronDown
                className={`size-4 transition-transform ${advancedOpen ? "rotate-180" : ""}`}
              />
              {t("infobases.form.advancedToggle")}
            </button>

            {advancedOpen && (
              <PublicationFieldset
                control={form.control}
                siteOptions={siteOptions}
                sitesState={sitesState}
                onRefreshSites={refetchSites}
                platformVersionOptions={platformVersionOptions}
                platformVersionsState={platformVersionsState}
                onRefreshPlatformVersions={refetchPlatformVersions}
                computedDefaultPath={computedDefaultPath}
                markNameTouched={markNameTouched}
                markVirtualPathTouched={markVirtualPathTouched}
                markPhysicalPathTouched={markPhysicalPathTouched}
              />
            )}

            <DialogFooter className="gap-2">
              <Button
                type="button"
                variant="ghost"
                disabled={pending}
                onClick={() => onOpenChange(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={pending}>
                {pending ? t("common.loading") : isEdit ? t("common.save") : t("common.create")}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
