import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge, type StatusBadgeVariant } from "@/components/ui/StatusBadge";
import { useMe } from "@/features/auth/useAuth";
import { RasServiceActionDialog } from "./RasServiceActionDialog";
import {
  useRasServiceStatus,
  type RasServiceOperation,
  type RasServiceStatus,
} from "./useRasService";

// Состояние → вариант бейджа. Незнакомое будущее состояние → нейтральный, без действия.
const STATE_VARIANT: Record<string, StatusBadgeVariant> = {
  Ok: "success",
  NotRegistered: "warning",
  Outdated: "warning",
  Stopped: "warning",
};

// Состояние → лечащая операция. Ok (нет действия) и неизвестное состояние → null.
const STATE_OPERATION: Record<string, RasServiceOperation | undefined> = {
  NotRegistered: "register",
  Outdated: "update",
  Stopped: "start",
};

// Блок состояния службы RAS в секции «Подключение к 1С/RAS» (MLC-160, ADR-47).
// Admin-only (как креды/endpoint там же). Ленивость (ревью MLC-159): /status НЕ
// дёргается на каждом заходе в Настройки — запрос включается только после раскрытия
// блока («Проверить состояние»); enabled передаётся в useRasServiceStatus. Статус —
// только через StatusBadge (инвариант ADR-46).
export function RasServiceCard() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  // Ленивый триггер: до первого «Проверить состояние» запрос не выполняется.
  const [checked, setChecked] = useState(false);
  const { data, isFetching, isError, refetch } = useRasServiceStatus(checked);

  const [dialogOp, setDialogOp] = useState<RasServiceOperation | null>(null);

  // Раздел Настроек видят и Viewer'ы, но действие меняет систему — блок только для Admin.
  if (!isAdmin) {
    return null;
  }

  const handleCheck = () => {
    if (checked) {
      // Повторная проверка — форс-перезапрос (минуя staleTime).
      void refetch();
    } else {
      setChecked(true);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("settings.rasService.title")}</CardTitle>
        <CardDescription>{t("settings.rasService.description")}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <div className="flex items-center gap-3">
          <Button size="sm" variant="outline" onClick={handleCheck} disabled={isFetching}>
            {isFetching ? t("common.loading") : t("settings.rasService.check")}
          </Button>
          {data && (
            <StatusBadge variant={STATE_VARIANT[data.state] ?? "neutral"}>
              {t(`settings.rasService.states.${data.state}`, { defaultValue: data.state })}
            </StatusBadge>
          )}
        </div>

        {isFetching && !data && <Skeleton className="h-20 w-full" />}

        {isError && (
          <p className="text-destructive text-sm">{t("settings.rasService.loadFailed")}</p>
        )}

        {data && !isFetching && <RasServiceBody status={data} onAct={(op) => setDialogOp(op)} />}
      </CardContent>

      {data && dialogOp && (
        <RasServiceActionDialog
          open={dialogOp !== null}
          onOpenChange={(open) => {
            if (!open) setDialogOp(null);
          }}
          operation={dialogOp}
          status={data}
        />
      )}
    </Card>
  );
}

function RasServiceBody({
  status,
  onAct,
}: {
  status: RasServiceStatus;
  onAct: (op: RasServiceOperation) => void;
}) {
  const { t } = useTranslation();
  const operation = STATE_OPERATION[status.state];

  return (
    <div className="grid gap-3 text-sm">
      {/* Ok — показываем имя службы / платформу / порт обнаружённой службы. */}
      {status.state === "Ok" && status.service && (
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
          <dt className="text-muted-foreground">{t("settings.rasService.fields.service")}</dt>
          <dd className="font-medium">{status.service.serviceName}</dd>
          {status.service.platformVersion && (
            <>
              <dt className="text-muted-foreground">{t("settings.rasService.fields.platform")}</dt>
              <dd className="font-medium">{status.service.platformVersion}</dd>
            </>
          )}
          {status.service.port && (
            <>
              <dt className="text-muted-foreground">{t("settings.rasService.fields.port")}</dt>
              <dd className="font-medium">{status.service.port}</dd>
            </>
          )}
        </dl>
      )}

      {/* Outdated — поясняем, что устарело (текущее ≠ целевое). */}
      {status.state === "Outdated" && (
        <p className="text-muted-foreground">
          {t("settings.rasService.outdatedHint", {
            currentVersion: status.service?.platformVersion ?? "—",
            currentPort: status.service?.port ?? "—",
            targetVersion: status.target?.platformVersion ?? "—",
            targetPort: status.target?.port ?? "—",
          })}
        </p>
      )}

      {/* targetReady=false — окружение не готово: показать issue, действие заблокировать. */}
      {!status.targetReady && status.issue && <p className="text-destructive">{status.issue}</p>}

      {/* Лечащая кнопка для NotRegistered/Outdated/Stopped. Для Outdated в подписи —
          целевая версия. Заблокирована, если окружение не готово (targetReady=false). */}
      {operation && (
        <div>
          <Button size="sm" disabled={!status.targetReady} onClick={() => onAct(operation)}>
            {operation === "update"
              ? t("settings.rasService.actions.update", {
                  version: status.target?.platformVersion ?? "—",
                })
              : t(`settings.rasService.actions.${operation}`)}
          </Button>
        </div>
      )}
    </div>
  );
}
