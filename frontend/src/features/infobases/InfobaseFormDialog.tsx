import { useEffect, useRef, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useSettings } from "@/features/settings/useSettings";
import { ChevronDown } from "lucide-react";
import { toast } from "sonner";
import { z } from "zod";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { ApiError, readConflictBody } from "@/lib/api";
import { DiscoveryField } from "@/features/discovery/DiscoveryField";
import {
  toDiscoveryState,
  useClusterInfobases,
  useDatabases,
  useIisSites,
  usePlatformVersions,
} from "@/features/discovery/useDiscovery";
import type { Tenant } from "@/features/tenants/types";
import type {
  CreateInfobaseInput,
  InfobaseListItem,
  InfobaseStatus,
  UpdateInfobaseInput,
} from "./types";
import { useCreateInfobase, useInfobases, useUpdateInfobase } from "./useInfobases";
import { physicalPathFromDatabase, virtualPathFromDatabase } from "./paths";

const PLATFORM_VERSION_PATTERN = /^\d+\.\d+\.\d+\.\d+$/;
const GUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const STATUSES: InfobaseStatus[] = ["Active", "Maintenance", "Suspended"];

function buildSchema(t: (k: string) => string) {
  return z.object({
    tenantId: z
      .string()
      .min(1, t("infobases.errors.tenantRequired"))
      .regex(GUID_PATTERN, t("infobases.errors.tenantRequired")),
    name: z
      .string()
      .trim()
      .min(1, t("infobases.errors.nameRequired"))
      .max(200, t("infobases.errors.nameTooLong")),
    clusterInfobaseId: z
      .string()
      .trim()
      .regex(GUID_PATTERN, t("infobases.errors.clusterIdInvalid")),
    databaseServer: z
      .string()
      .trim()
      .min(1, t("infobases.errors.databaseServerRequired"))
      .max(200, t("infobases.errors.fieldTooLong")),
    databaseName: z
      .string()
      .trim()
      .min(1, t("infobases.errors.databaseNameRequired"))
      .max(200, t("infobases.errors.fieldTooLong")),
    status: z.enum(STATUSES),
    publication: z.object({
      siteName: z
        .string()
        .trim()
        .min(1, t("publications.errors.siteNameRequired"))
        .max(200, t("infobases.errors.fieldTooLong")),
      virtualPath: z
        .string()
        .trim()
        .min(1, t("publications.errors.virtualPathRequired"))
        .startsWith("/", t("publications.errors.virtualPathLeadingSlash"))
        .refine((v) => !/\s/.test(v), t("publications.errors.virtualPathNoSpaces")),
      platformVersion: z
        .string()
        .trim()
        .min(1, t("publications.errors.platformVersionRequired"))
        .regex(PLATFORM_VERSION_PATTERN, t("publications.errors.platformVersionFormat")),
      enableOData: z.boolean(),
      enableHttpServices: z.boolean(),
      vrdCustomXml: z.string().optional(),
      physicalPathOverride: z
        .string()
        .max(260, t("publications.errors.physicalPathOverrideTooLong"))
        .optional(),
    }),
  });
}

type FormValues = z.infer<ReturnType<typeof buildSchema>>;

// Поля, которые живут в свёрнутом блоке «Дополнительно». Если валидация падает
// на одном из них, блок надо раскрыть, иначе пользователь не увидит ошибку.
const ADVANCED_ERROR_KEYS = new Set(["name", "databaseServer", "status", "publication"]);

interface InfobaseFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  infobase?: InfobaseListItem | null;
  tenants: Tenant[];
  defaultTenantId?: string;
}

export function InfobaseFormDialog({
  open,
  onOpenChange,
  infobase,
  tenants,
  defaultTenantId,
}: InfobaseFormDialogProps) {
  const { t } = useTranslation();
  const isEdit = Boolean(infobase);

  const create = useCreateInfobase();
  const update = useUpdateInfobase();

  const { data: settings } = useSettings();
  const settingValue = (key: string) => settings?.find((s) => s.key === key)?.value ?? undefined;
  const defaultDatabaseServer = settingValue("Defaults.DatabaseServer") ?? "";
  const defaultSiteName = settingValue("IIS.DefaultSiteName") ?? "Default Web Site";
  const defaultPlatformVersion = settingValue("OneC.DefaultPlatformVersion") ?? "";
  const defaultVrdRoot = settingValue("IIS.DefaultVrdRoot") ?? "C:\\inetpub\\wwwroot";

  // Блок «Дополнительно» свёрнут по умолчанию — основная цель упрощённой формы.
  const [advancedOpen, setAdvancedOpen] = useState(false);

  // Автоподстановка названия из базы кластера и виртуального пути из имени БД
  // работает, только пока пользователь не правил поле руками. В edit-режиме
  // значения уже заданы — считаем их «тронутыми», чтобы не перетирать.
  const nameTouched = useRef(isEdit);
  const virtualPathTouched = useRef(isEdit);
  const physicalPathTouched = useRef(isEdit);
  const settingsApplied = useRef(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(buildSchema(t)),
    defaultValues: infobase
      ? {
          tenantId: infobase.tenantId,
          name: infobase.name,
          clusterInfobaseId: infobase.clusterInfobaseId,
          databaseServer: infobase.databaseServer,
          databaseName: infobase.databaseName,
          status: infobase.status,
          publication: {
            siteName: infobase.publication.siteName,
            virtualPath: infobase.publication.virtualPath,
            platformVersion: infobase.publication.platformVersion,
            enableOData: infobase.publication.enableOData,
            enableHttpServices: infobase.publication.enableHttpServices,
            vrdCustomXml: infobase.publication.vrdCustomXml ?? "",
            physicalPathOverride: infobase.publication.physicalPathOverride ?? "",
          },
        }
      : {
          tenantId: defaultTenantId ?? tenants[0]?.id ?? "",
          name: "",
          clusterInfobaseId: "",
          databaseServer: defaultDatabaseServer,
          databaseName: "",
          status: "Active",
          publication: {
            siteName: defaultSiteName,
            virtualPath: "",
            platformVersion: defaultPlatformVersion,
            enableOData: false,
            enableHttpServices: false,
            vrdCustomXml: "",
            physicalPathOverride: "",
          },
        },
  });

  // Настройки грузятся асинхронно — на момент mount'а формы дефолтов могло ещё
  // не быть. Когда они приходят, подставляем их в незаполненные поля один раз
  // (только при создании и только если пользователь их не трогал).
  useEffect(() => {
    if (isEdit || settingsApplied.current || !settings) return;
    settingsApplied.current = true;
    if (!form.getValues("databaseServer")) {
      form.setValue("databaseServer", defaultDatabaseServer);
    }
    if (!form.getValues("publication.platformVersion")) {
      form.setValue("publication.platformVersion", defaultPlatformVersion);
    }
    const site = form.getValues("publication.siteName");
    if (!site || site === "Default Web Site") {
      form.setValue("publication.siteName", defaultSiteName);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settings, isEdit]);

  const [watchedDatabaseServer, watchedDatabaseName] = useWatch({
    control: form.control,
    name: ["databaseServer", "databaseName"],
  });

  // Discovery: тянем списки, пока диалог открыт (ленивая загрузка по `open`).
  const infobasesQuery = useClusterInfobases(open);
  const sitesQuery = useIisSites(open);
  const databasesQuery = useDatabases(watchedDatabaseServer ?? "", open);
  const platformVersionsQuery = usePlatformVersions(open);

  const infobasesState = toDiscoveryState(infobasesQuery);
  const sitesState = toDiscoveryState(sitesQuery);
  const databasesState = toDiscoveryState(databasesQuery);
  const platformVersionsState = toDiscoveryState(platformVersionsQuery);

  // Базы кластера, уже привязанные к любому клиенту, не предлагаем — одна база
  // принадлежит только одному клиенту. Свою базу в режиме редактирования не исключаем.
  const allInfobasesQuery = useInfobases();
  const takenClusterIds = new Set(
    (allInfobasesQuery.data?.items ?? [])
      .filter((ib) => ib.id !== infobase?.id)
      .map((ib) => ib.clusterInfobaseId)
  );

  const infobaseOptions = (infobasesQuery.data?.items ?? [])
    .filter((i) => !takenClusterIds.has(i.id))
    .map((i) => ({
      value: i.id,
      label: i.name,
      hint: i.description,
    }));
  const siteOptions = (sitesQuery.data?.items ?? []).map((s) => ({
    value: s.siteName,
    label: s.siteName,
  }));
  const databaseOptions = (databasesQuery.data?.items ?? []).map((d) => ({
    value: d,
    label: d,
  }));
  const platformVersionOptions = (platformVersionsQuery.data?.items ?? []).map((v) => ({
    value: v.version,
    label: v.version,
    hint: v.architecture,
  }));

  // Выбор базы кластера по имени — подставляем имя как название инфобазы.
  const handleClusterChange = (value: string, onChange: (v: string) => void) => {
    onChange(value);
    if (nameTouched.current) return;
    const picked = infobaseOptions.find((o) => o.value === value);
    if (picked) {
      form.setValue("name", picked.label, { shouldValidate: true });
    }
  };

  // Выбор/ввод имени БД — генерируем из него виртуальный и физический путь
  // публикации (каждый — только пока пользователь его не правил руками).
  const handleDatabaseNameChange = (value: string, onChange: (v: string) => void) => {
    onChange(value);
    if (!virtualPathTouched.current) {
      const vp = virtualPathFromDatabase(value);
      if (vp) {
        form.setValue("publication.virtualPath", vp, { shouldValidate: true });
      }
    }
    if (!physicalPathTouched.current) {
      const pp = physicalPathFromDatabase(defaultVrdRoot, value);
      form.setValue("publication.physicalPathOverride", pp, { shouldValidate: true });
    }
  };

  const computedDefaultPath = (() => {
    const pp = physicalPathFromDatabase(defaultVrdRoot, watchedDatabaseName ?? "");
    return pp || t("publications.form.physicalPathOverridePlaceholderGeneric");
  })();

  const onSubmit = form.handleSubmit(
    async (values) => {
      const publicationInput = {
        siteName: values.publication.siteName.trim(),
        virtualPath: values.publication.virtualPath.trim(),
        platformVersion: values.publication.platformVersion.trim(),
        enableOData: values.publication.enableOData,
        enableHttpServices: values.publication.enableHttpServices,
        vrdCustomXml: values.publication.vrdCustomXml?.trim()
          ? values.publication.vrdCustomXml.trim()
          : null,
        physicalPathOverride: values.publication.physicalPathOverride?.trim() || null,
      };

      try {
        if (infobase) {
          const input: UpdateInfobaseInput = {
            name: values.name.trim(),
            clusterInfobaseId: values.clusterInfobaseId.trim(),
            databaseServer: values.databaseServer.trim(),
            databaseName: values.databaseName.trim(),
            status: values.status,
            publication: publicationInput,
          };
          await update.mutateAsync({ id: infobase.id, input });
          toast.success(t("infobases.toasts.updated", { name: input.name }));
        } else {
          const input: CreateInfobaseInput = {
            tenantId: values.tenantId,
            name: values.name.trim(),
            clusterInfobaseId: values.clusterInfobaseId.trim(),
            databaseServer: values.databaseServer.trim(),
            databaseName: values.databaseName.trim(),
            status: values.status,
            publication: publicationInput,
          };
          await create.mutateAsync(input);
          toast.success(t("infobases.toasts.created", { name: input.name }));
        }
        onOpenChange(false);
      } catch (error) {
        if (error instanceof ApiError) {
          if (error.status === 409) {
            const body = readConflictBody(error);
            if (body?.code === "NAME_DUPLICATE_IN_TENANT") {
              setAdvancedOpen(true);
              form.setError("name", {
                type: "server",
                message: t("infobases.errors.nameDuplicate"),
              });
              return;
            }
            if (body?.code === "INFOBASE_ALREADY_ASSIGNED") {
              form.setError("clusterInfobaseId", {
                type: "server",
                message: t("infobases.errors.clusterAlreadyAssigned"),
              });
              return;
            }
          }
          if (error.status === 404) {
            form.setError("tenantId", {
              type: "server",
              message: t("infobases.errors.tenantNotFound"),
            });
            return;
          }
          if (error.status === 400) {
            toast.error(error.message || t("errors.generic"));
            return;
          }
        }
        toast.error(t("errors.generic"));
      }
    },
    (errors) => {
      // Раскрываем «Дополнительно», если ошибка валидации в одном из его полей.
      if (Object.keys(errors).some((k) => ADVANCED_ERROR_KEYS.has(k))) {
        setAdvancedOpen(true);
      }
    }
  );

  const pending = create.isPending || update.isPending;

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
                      onRefresh={() => void infobasesQuery.refetch()}
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
                      onRefresh={() => void databasesQuery.refetch()}
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
              <div className="grid gap-5">
                <p className="text-muted-foreground text-xs">{t("infobases.form.advancedHint")}</p>

                {/* Группа: Инфобаза (название + статус) */}
                <div className="grid gap-4">
                  <div className="space-y-0.5">
                    <h4 className="text-sm font-semibold">{t("infobases.form.groupInfobase")}</h4>
                    <p className="text-muted-foreground text-xs">
                      {t("infobases.form.groupInfobaseHint")}
                    </p>
                  </div>

                  <FormField
                    control={form.control}
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
                              nameTouched.current = true;
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
                    control={form.control}
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

                {/* Группа: СУБД (SQL Server) */}
                <div className="grid gap-4">
                  <div className="space-y-0.5">
                    <h4 className="text-sm font-semibold">{t("infobases.form.groupDatabase")}</h4>
                    <p className="text-muted-foreground text-xs">
                      {t("infobases.form.groupDatabaseHint")}
                    </p>
                  </div>

                  <FormField
                    control={form.control}
                    name="databaseServer"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{t("infobases.form.databaseServerLabel")}</FormLabel>
                        <FormControl>
                          <Input
                            autoComplete="off"
                            placeholder={t("infobases.form.databaseServerPlaceholder")}
                            {...field}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                <Separator />

                {/* Группа: Публикация в IIS */}
                <div className="space-y-0.5">
                  <h4 className="text-sm font-semibold">{t("infobases.form.groupPublication")}</h4>
                  <p className="text-muted-foreground text-xs">
                    {t("infobases.form.groupPublicationHint")}
                  </p>
                </div>

                <div className="grid gap-4 sm:grid-cols-2">
                  <FormField
                    control={form.control}
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
                            onRefresh={() => void sitesQuery.refetch()}
                            manualPlaceholder="Default Web Site"
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
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
                              virtualPathTouched.current = true;
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
                  control={form.control}
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
                          onRefresh={() => void platformVersionsQuery.refetch()}
                          manualPlaceholder="8.3.23.1865"
                          inputClassName="font-mono text-xs"
                        />
                      </FormControl>
                      <FormDescription>
                        {t("publications.form.platformVersionHint")}
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
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
                            physicalPathTouched.current = true;
                            field.onChange(e);
                          }}
                        />
                      </FormControl>
                      <FormDescription>
                        {t("publications.form.physicalPathOverrideHint")}
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <div className="grid gap-3 sm:grid-cols-2">
                  <FormField
                    control={form.control}
                    name="publication.enableOData"
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
                          <Label htmlFor="publication-oData" className="font-medium">
                            {t("publications.fields.enableOData")}
                          </Label>
                          <input
                            id="publication-oData"
                            type="checkbox"
                            className="size-4 cursor-pointer"
                            checked={field.value}
                            onChange={(e) => field.onChange(e.target.checked)}
                          />
                        </div>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="publication.enableHttpServices"
                    render={({ field }) => (
                      <FormItem>
                        <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
                          <Label htmlFor="publication-http" className="font-medium">
                            {t("publications.fields.enableHttpServices")}
                          </Label>
                          <input
                            id="publication-http"
                            type="checkbox"
                            className="size-4 cursor-pointer"
                            checked={field.value}
                            onChange={(e) => field.onChange(e.target.checked)}
                          />
                        </div>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
              </div>
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
