import { useEffect, useRef, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { toastFormSubmitError } from "@/lib/apiErrors";
import { useSettings } from "@/features/settings/useSettings";
import {
  toDiscoveryState,
  useClusterInfobases,
  useDatabases,
  useIisSites,
  usePlatformVersions,
} from "@/features/discovery/useDiscovery";
import type { Tenant } from "@/features/tenants/types";
import type { CreateInfobaseInput, InfobaseListItem, UpdateInfobaseInput } from "./types";
import { useClusterIdAvailability, useCreateInfobase, useUpdateInfobase } from "./useInfobases";
import { physicalPathFromDatabase, virtualPathFromDatabase } from "./paths";
import { buildInfobaseFormSchema, type InfobaseFormValues } from "./validation";
import { mapConflictToField } from "./mapConflictToField";

type FormValues = InfobaseFormValues;

// Поля, которые живут в свёрнутом блоке «Дополнительно». Если валидация падает
// на одном из них, блок надо раскрыть, иначе пользователь не увидит ошибку.
// databaseServer здесь нет — поле скрытое (MLC-082), его ошибка показывается toast'ом.
const ADVANCED_ERROR_KEYS = new Set(["name", "status", "publication"]);

interface UseInfobaseFormArgs {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  infobase?: InfobaseListItem | null;
  tenants: Tenant[];
  defaultTenantId?: string;
}

// MLC-023 — вся не-презентационная логика формы инфобазы (схема, defaultValues, prefill
// настроек, touched-рефы автоподстановки, discovery, точечная проверка занятости, submit,
// маппинг 409). InfobaseFormDialog остаётся тонким вью поверх этого хука.
// MLC-082 (single-host): сервер СУБД в форме не показывается. databaseServer — скрытое
// поле: при создании — из настройки Defaults.DatabaseServer, при редактировании —
// текущее значение базы (правка не мигрирует сервер молча). Контракт API прежний.
export function useInfobaseForm({
  open,
  onOpenChange,
  infobase,
  tenants,
  defaultTenantId,
}: UseInfobaseFormArgs) {
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
    resolver: zodResolver(buildInfobaseFormSchema(t)),
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
  // Список БД дёргается сразу с сервером из скрытого поля (настройка/значение базы) —
  // двухшаговый выбор «сначала сервер, потом база» исчез (MLC-082).
  const infobasesQuery = useClusterInfobases(open);
  const sitesQuery = useIisSites(open);
  const databasesQuery = useDatabases(watchedDatabaseServer ?? "", open);
  const platformVersionsQuery = usePlatformVersions(open);

  const infobasesState = toDiscoveryState(infobasesQuery);
  const sitesState = toDiscoveryState(sitesQuery);
  const databasesState = toDiscoveryState(databasesQuery);
  const platformVersionsState = toDiscoveryState(platformVersionsQuery);

  const infobaseOptions = (infobasesQuery.data?.items ?? []).map((i) => ({
    value: i.id,
    label: i.name,
    hint: i.description,
  }));

  // MLC-015 — занятость выбранной базы кластера проверяем точечно (а не выгружая весь
  // список инфобаз при каждом открытии формы). Запрос идёт при валидном GUID; свою базу
  // в режиме редактирования исключаем через excludeId. 409 на submit остаётся backstop'ом.
  const watchedClusterId = useWatch({ control: form.control, name: "clusterInfobaseId" });
  const clusterAvailability = useClusterIdAvailability(
    (watchedClusterId ?? "").trim(),
    infobase?.id,
    open
  );

  useEffect(() => {
    const data = clusterAvailability.data;
    if (!data) return;
    if (data.taken) {
      form.setError("clusterInfobaseId", {
        type: "server",
        message: data.takenByTenantName
          ? t("infobases.errors.clusterAlreadyAssignedNamed", { name: data.takenByTenantName })
          : t("infobases.errors.clusterAlreadyAssigned"),
      });
    } else if (form.getFieldState("clusterInfobaseId").error?.type === "server") {
      // База свободна — снимаем только нашу серверную подсказку, не трогая zod-ошибки.
      form.clearErrors("clusterInfobaseId");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clusterAvailability.data]);

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
      // Точечная проверка занятости уже показала конфликт — не делаем заведомо обречённый
      // запрос (на сервере его всё равно перехватит 409-backstop).
      if (clusterAvailability.data?.taken) {
        form.setError("clusterInfobaseId", {
          type: "server",
          message: clusterAvailability.data.takenByTenantName
            ? t("infobases.errors.clusterAlreadyAssignedNamed", {
                name: clusterAvailability.data.takenByTenantName,
              })
            : t("infobases.errors.clusterAlreadyAssigned"),
        });
        return;
      }

      const publicationInput = {
        siteName: values.publication.siteName.trim(),
        virtualPath: values.publication.virtualPath.trim(),
        platformVersion: values.publication.platformVersion.trim(),
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
        const mapped = mapConflictToField(error);
        if (mapped) {
          if (mapped.openAdvanced) {
            setAdvancedOpen(true);
          }
          form.setError(mapped.field, { type: "server", message: t(mapped.messageKey) });
          return;
        }
        toastFormSubmitError(error, t);
      }
    },
    (errors) => {
      // databaseServer — скрытое поле: его ошибку (настройка Defaults.DatabaseServer
      // не задана) пользователь в форме не увидит — показываем toast с подсказкой.
      if (errors.databaseServer) {
        toast.error(t("infobases.errors.databaseServerNotConfigured"));
      }
      // Раскрываем «Дополнительно», если ошибка валидации в одном из его полей.
      if (Object.keys(errors).some((k) => ADVANCED_ERROR_KEYS.has(k))) {
        setAdvancedOpen(true);
      }
    }
  );

  const pending = create.isPending || update.isPending;

  return {
    form,
    isEdit,
    pending,
    advancedOpen,
    setAdvancedOpen,
    onSubmit,
    // discovery — видимые поля
    infobaseOptions,
    infobasesState,
    refetchInfobases: () => void infobasesQuery.refetch(),
    databaseOptions,
    databasesState,
    refetchDatabases: () => void databasesQuery.refetch(),
    // discovery — блок «Дополнительно»
    siteOptions,
    sitesState,
    refetchSites: () => void sitesQuery.refetch(),
    platformVersionOptions,
    platformVersionsState,
    refetchPlatformVersions: () => void platformVersionsQuery.refetch(),
    // авто-подстановка / производные
    watchedDatabaseServer,
    computedDefaultPath,
    handleClusterChange,
    handleDatabaseNameChange,
    // touched-маркеры для полей блока «Дополнительно»
    markNameTouched: () => {
      nameTouched.current = true;
    },
    markVirtualPathTouched: () => {
      virtualPathTouched.current = true;
    },
    markPhysicalPathTouched: () => {
      physicalPathTouched.current = true;
    },
  };
}

export type UseInfobaseFormResult = ReturnType<typeof useInfobaseForm>;
