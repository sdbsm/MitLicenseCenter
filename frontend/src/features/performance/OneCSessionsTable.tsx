import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge } from "@/components/ui/StatusBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import {
  classifySession,
  formatMs,
  formatSignedMb,
  SESSION_STATE_VARIANT,
  shortUuid,
  sortSessionsByLoad,
  type SessionState,
} from "./onecLoad";
import type { OneCSessionLoad } from "./types";

interface OneCSessionsTableProps {
  sessions: OneCSessionLoad[];
  capturedAtUtc: string;
}

/**
 * Топ сеансов 1С «кто грузит сейчас» (MLC-067) — отдельно от `/sessions` (там задача kill).
 * Сортировка по `cpu-time-current`/`duration-current`; подсветка заблокированных, долгих/
 * зависших и молчащих через бейдж состояния. Колонка СУБД отделяет «1С грузит app-сервер»
 * от «1С грузит SQL» (`duration-current-dbms`). Отсутствующие perf-поля — «—», не 0.
 */
export function OneCSessionsTable({ sessions, capturedAtUtc }: OneCSessionsTableProps) {
  const { t } = useTranslation();
  const rows = useMemo(() => sortSessionsByLoad(sessions), [sessions]);

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-16">{t("performance.onec.sessions.number")}</TableHead>
            <TableHead>{t("performance.onec.sessions.user")}</TableHead>
            <TableHead>{t("performance.onec.sessions.host")}</TableHead>
            <TableHead className="w-20">{t("performance.onec.sessions.app")}</TableHead>
            <TableHead className="w-24 text-right">{t("performance.onec.sessions.cpu")}</TableHead>
            <TableHead className="w-24 text-right">
              {t("performance.onec.sessions.duration")}
            </TableHead>
            <TableHead className="w-24 text-right">{t("performance.onec.sessions.dbms")}</TableHead>
            <TableHead className="w-24 text-right">
              {t("performance.onec.sessions.memory")}
            </TableHead>
            <TableHead className="w-28">{t("performance.onec.sessions.lastActive")}</TableHead>
            <TableHead className="w-32">{t("performance.onec.sessions.state")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.length === 0 ? (
            <TableRow>
              <TableCell colSpan={10} className="text-muted-foreground py-8 text-center text-sm">
                {t("performance.onec.sessions.empty")}
              </TableCell>
            </TableRow>
          ) : (
            rows.map((s) => (
              <SessionRow key={s.sessionId} session={s} capturedAtUtc={capturedAtUtc} />
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}

function SessionRow({
  session,
  capturedAtUtc,
}: {
  session: OneCSessionLoad;
  capturedAtUtc: string;
}) {
  const { t } = useTranslation();
  const state: SessionState = classifySession(session, capturedAtUtc);

  return (
    <TableRow>
      <TableCell className="tabular-nums">{session.sessionNumber ?? "—"}</TableCell>
      <TableCell className="font-medium">
        {session.userName.trim() ? (
          session.userName
        ) : (
          <span className="text-muted-foreground italic">{t("performance.onec.noUser")}</span>
        )}
      </TableCell>
      <TableCell className="text-muted-foreground text-sm">{session.host || "—"}</TableCell>
      <TableCell className="font-mono text-xs">{session.appId}</TableCell>
      <TableCell className="text-right tabular-nums">{formatMs(session.cpuTimeCurrent)}</TableCell>
      <TableCell className="text-right tabular-nums">{formatMs(session.durationCurrent)}</TableCell>
      <TableCell className="text-muted-foreground text-right tabular-nums">
        {formatMs(session.durationCurrentDbms)}
      </TableCell>
      <TableCell className="text-right tabular-nums">
        {formatSignedMb(session.memoryCurrent)}
      </TableCell>
      <TableCell className="text-sm">
        {session.lastActiveAtUtc ? (
          <RelativeTime value={session.lastActiveAtUtc} />
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </TableCell>
      <TableCell>
        <Tooltip>
          <TooltipTrigger asChild>
            <span>
              <StatusBadge variant={SESSION_STATE_VARIANT[state]}>
                {t(`performance.onec.state.${state}`)}
              </StatusBadge>
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <span className="font-mono text-xs">
              {t("performance.onec.sessions.process")}: {shortUuid(session.process)}
            </span>
          </TooltipContent>
        </Tooltip>
      </TableCell>
    </TableRow>
  );
}
