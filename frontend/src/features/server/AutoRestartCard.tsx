import { useState } from "react";
import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { ApiError } from "@/lib/api";
import { useMe } from "@/features/auth/useAuth";
import {
  useAutoRestartSchedule,
  useSetAutoRestartSchedule,
  type AutoRestartSchedule,
} from "./useAutoRestartSchedule";

// Время HH:mm (00:00–23:59) — зеркало BE-регекса AutoRestartTimeRegex (ServerEndpoints).
const TIME_PATTERN = /^([01]?\d|2[0-3]):[0-5]\d$/;

/**
 * Карточка «Расписание авто-рестартов» во вкладке «Службы» раздела «Сервер» (MLC-218,
 * ADR-55): оператор задаёт ночной профилактический рестарт службы сервера 1С. Тумблер
 * «включено» + поле времени (ЧЧ:мм по часам хоста) + строка «прошлый прогон». Сохранение —
 * только Admin (роль-гейт как у OneCServerActionDialog); Viewer видит состояние без правки.
 */
export function AutoRestartCard() {
  const { t } = useTranslation();
  const { data, isLoading } = useAutoRestartSchedule();

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("server.autoRestart.title")}</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading && !data ? (
          <p className="text-muted-foreground text-sm">{t("common.loading")}</p>
        ) : data ? (
          // key по сохранённому состоянию: при изменении расписания (после успешного
          // сохранения/рефетча) форма пересоздаётся с актуальными начальными значениями —
          // без useEffect-синхронизации (cascading renders) поверх локального стейта.
          <AutoRestartForm key={`${data.enabled}-${data.time}`} schedule={data} />
        ) : (
          <p className="text-muted-foreground text-sm">{t("server.autoRestart.loadFailed")}</p>
        )}
      </CardContent>
    </Card>
  );
}

function AutoRestartForm({ schedule }: { schedule: AutoRestartSchedule }) {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;
  const mutation = useSetAutoRestartSchedule();

  // Локальная форма поверх серверного состояния. Начальные значения берутся из props;
  // ресинхронизация при изменении сохранённого расписания — через key на компоненте (см.
  // AutoRestartCard), а не useEffect (cascading renders).
  const [enabled, setEnabled] = useState(schedule.enabled);
  const [time, setTime] = useState(schedule.time);

  const timeValid = TIME_PATTERN.test(time.trim());
  const dirty = enabled !== schedule.enabled || time.trim() !== schedule.time;
  const canSave = isAdmin && timeValid && dirty && !mutation.isPending;

  const handleSave = async () => {
    try {
      await mutation.mutateAsync({ enabled, time: time.trim() });
      toast.success(t("server.autoRestart.saved"));
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        toast.error(t("server.autoRestart.invalidTime"));
        return;
      }
      toast.error(t("errors.generic"));
    }
  };

  return (
    <div className="grid gap-4">
      <p className="text-muted-foreground text-sm">{t("server.autoRestart.description")}</p>

      {/* Тумблер «включено» — Admin переключает, Viewer видит состояние (disabled). */}
      <div className="flex items-center gap-3">
        <Switch
          id="auto-restart-enabled"
          checked={enabled}
          onCheckedChange={setEnabled}
          disabled={!isAdmin || mutation.isPending}
        />
        <Label htmlFor="auto-restart-enabled">{t("server.autoRestart.enabledLabel")}</Label>
      </div>

      {/* Время суток ЧЧ:мм по часам хоста. */}
      <div className="grid gap-1.5">
        <Label htmlFor="auto-restart-time">{t("server.autoRestart.timeLabel")}</Label>
        <Input
          id="auto-restart-time"
          type="time"
          value={time}
          onChange={(e) => setTime(e.target.value)}
          disabled={!isAdmin || mutation.isPending}
          aria-invalid={!timeValid}
          className="w-32"
        />
        {!timeValid && (
          <p className="text-status-danger text-xs">{t("server.autoRestart.invalidTime")}</p>
        )}
        <p className="text-muted-foreground text-xs">{t("server.autoRestart.timeHint")}</p>
      </div>

      {/* Прошлый прогон. */}
      <p className="text-muted-foreground text-sm">
        {t("server.autoRestart.lastRun", { value: fmtLastRun(schedule.lastRunUtc, t) })}
      </p>

      {/* Целевые службы (что рестартнётся) — текущие запущенные ragent. */}
      {schedule.targetServices.length > 0 ? (
        <p className="text-muted-foreground text-xs">
          {t("server.autoRestart.targets", { value: schedule.targetServices.join(", ") })}
        </p>
      ) : (
        <p className="text-muted-foreground text-xs">{t("server.autoRestart.noTargets")}</p>
      )}

      {/* Сохранение — только Admin. */}
      {isAdmin && (
        <div>
          <Button onClick={() => void handleSave()} disabled={!canSave}>
            {mutation.isPending ? t("common.loading") : t("server.autoRestart.save")}
          </Button>
        </div>
      )}
    </div>
  );
}

// ISO-8601 UTC → локальная дата-время оператора; null → «ещё не запускалась».
function fmtLastRun(iso: string | null, t: (key: string) => string): string {
  return iso
    ? format(new Date(iso), "dd.MM.yyyy HH:mm", { locale: ru })
    : t("server.autoRestart.never");
}
