import type { Control } from "react-hook-form";
import { useTranslation } from "react-i18next";
import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { DiscoveryField, type DiscoveryOption } from "@/features/discovery/DiscoveryField";
import type { DiscoveryState } from "@/features/discovery/useDiscovery";
import { STATUSES, type InfobaseFormValues } from "./validation";

interface PublicationFieldsetProps {
  control: Control<InfobaseFormValues>;
  // Discovery: сайт IIS, версия платформы.
  siteOptions: DiscoveryOption[];
  sitesState: DiscoveryState;
  onRefreshSites: () => void;
  platformVersionOptions: DiscoveryOption[];
  platformVersionsState: DiscoveryState;
  onRefreshPlatformVersions: () => void;
  // Placeholder физ. пути, вычисленный из имени БД (или generic).
  computedDefaultPath: string;
  // touched-маркеры — вызываются перед field.onChange, чтобы отключить автоподстановку
  // соответствующего поля (поведение 1:1 с прежним InfobaseFormDialog).
  markNameTouched: () => void;
  markVirtualPathTouched: () => void;
  markPhysicalPathTouched: () => void;
}

// MLC-023 — презентационный блок «Дополнительно» (2 группы: Инфобаза, Публикация в
// IIS). Без состояния и эффектов: получает control формы и discovery-пропсы от
// useInfobaseForm. Раскрытие/сворачивание блока остаётся в InfobaseFormDialog.
// Сервер СУБД в форме не показывается (MLC-082, single-host) — значение подставляется
// из настройки в useInfobaseForm.
export function PublicationFieldset({
  control,
  siteOptions,
  sitesState,
  onRefreshSites,
  platformVersionOptions,
  platformVersionsState,
  onRefreshPlatformVersions,
  computedDefaultPath,
  markNameTouched,
  markVirtualPathTouched,
  markPhysicalPathTouched,
}: PublicationFieldsetProps) {
  const { t } = useTranslation();

  return (
    <div className="grid gap-5">
      <p className="text-muted-foreground text-xs">{t("infobases.form.advancedHint")}</p>

      {/* Группа: Инфобаза (название + статус) */}
      <div className="grid gap-4">
        <div className="space-y-0.5">
          <h4 className="text-sm font-semibold">{t("infobases.form.groupInfobase")}</h4>
          <p className="text-muted-foreground text-xs">{t("infobases.form.groupInfobaseHint")}</p>
        </div>

        <FormField
          control={control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("infobases.fields.name")}</FormLabel>
              <FormControl>
                <Input
                  autoComplete="off"
                  placeholder={t("infobases.form.namePlaceholder")}
                  {...field}
                  onChange={(e) => {
                    markNameTouched();
                    field.onChange(e);
                  }}
                />
              </FormControl>
              <FormDescription>{t("infobases.form.nameHint")}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={control}
          name="status"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("infobases.fields.status")}</FormLabel>
              <Select value={field.value} onValueChange={field.onChange}>
                <FormControl>
                  <SelectTrigger className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  {STATUSES.map((status) => (
                    <SelectItem key={status} value={status}>
                      {t(`infobases.status.${status}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>

      <Separator />

      {/* Группа: Публикация в IIS */}
      <div className="space-y-0.5">
        <h4 className="text-sm font-semibold">{t("infobases.form.groupPublication")}</h4>
        <p className="text-muted-foreground text-xs">{t("infobases.form.groupPublicationHint")}</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          control={control}
          name="publication.siteName"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("publications.fields.siteName")}</FormLabel>
              <FormControl>
                <DiscoveryField
                  value={field.value}
                  onChange={field.onChange}
                  options={siteOptions}
                  available={sitesState.available}
                  loading={sitesState.loading}
                  error={sitesState.error}
                  onRefresh={onRefreshSites}
                  manualPlaceholder="Default Web Site"
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={control}
          name="publication.virtualPath"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("publications.fields.virtualPath")}</FormLabel>
              <FormControl>
                <Input
                  autoComplete="off"
                  placeholder="/acme-bp"
                  className="font-mono text-xs"
                  {...field}
                  onChange={(e) => {
                    markVirtualPathTouched();
                    field.onChange(e);
                  }}
                />
              </FormControl>
              <FormDescription>{t("publications.form.virtualPathHint")}</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>

      <FormField
        control={control}
        name="publication.platformVersion"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t("publications.fields.platformVersion")}</FormLabel>
            <FormControl>
              <DiscoveryField
                value={field.value}
                onChange={field.onChange}
                options={platformVersionOptions}
                available={platformVersionsState.available}
                loading={platformVersionsState.loading}
                error={platformVersionsState.error}
                onRefresh={onRefreshPlatformVersions}
                manualPlaceholder="8.3.23.1865"
                inputClassName="font-mono text-xs"
              />
            </FormControl>
            <FormDescription>{t("publications.form.platformVersionHint")}</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />

      <FormField
        control={control}
        name="publication.physicalPathOverride"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t("publications.fields.physicalPathOverride")}</FormLabel>
            <FormControl>
              <Input
                autoComplete="off"
                placeholder={computedDefaultPath}
                className="font-mono text-xs"
                {...field}
                value={field.value ?? ""}
                onChange={(e) => {
                  markPhysicalPathTouched();
                  field.onChange(e);
                }}
              />
            </FormControl>
            <FormDescription>{t("publications.form.physicalPathOverrideHint")}</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />
    </div>
  );
}
