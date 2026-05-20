import { format } from "date-fns";
import { ru } from "date-fns/locale";
import { MonitorPlayIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { RelativeTime } from "@/components/ui/RelativeTime";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useMe } from "@/features/auth/useAuth";
import { useInfobases } from "@/features/infobases/useInfobases";
import { KillSessionDialog } from "./KillSessionDialog";
import type { SessionSnapshotEntry } from "./types";
import { useSessionsSnapshot } from "./useSessionsSnapshot";

function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${seconds}с`;
  }
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    const secs = seconds % 60;
    return secs > 0 ? `${minutes}м ${secs}с` : `${minutes}м`;
  }
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}ч ${mins}м` : `${hours}ч`;
}

function parseParams(params: URLSearchParams) {
  return {
    q: params.get("q") ?? "",
    infobaseId: params.get("infobaseId") ?? "",
  };
}

export function SessionsPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { q, infobaseId } = useMemo(() => parseParams(searchParams), [searchParams]);

  const { data, isLoading, isError, refetch, failureCount } = useSessionsSnapshot();
  const { data: infobasesData } = useInfobases();
  const { data: me } = useMe();
  const isAdmin = me?.roles.includes("Admin") ?? false;

  const [selectedSession, setSelectedSession] = useState<SessionSnapshotEntry | null>(null);
  const [killOpen, setKillOpen] = useState(false);

  const infobaseById = useMemo(() => {
    const map = new Map<string, string>();
    for (const ib of infobasesData?.items ?? []) {
      map.set(ib.id, ib.name);
    }
    return map;
  }, [infobasesData]);

  const filtered = useMemo(() => {
    let rows = data?.items ?? [];
    if (q) {
      const lq = q.toLowerCase();
      rows = rows.filter(
        (r) => r.tenantName.toLowerCase().includes(lq) || r.userName.toLowerCase().includes(lq)
      );
    }
    if (infobaseId) {
      const name = infobaseById.get(infobaseId);
      if (name) {
        rows = rows.filter((r) => r.infobaseName === name);
      }
    }
    return rows;
  }, [data, q, infobaseId, infobaseById]);

  const setFilter = (next: { q?: string; infobaseId?: string }) => {
    const params = new URLSearchParams();
    const newQ = next.q !== undefined ? next.q : q;
    const newInfobaseId = next.infobaseId !== undefined ? next.infobaseId : infobaseId;
    if (newQ) params.set("q", newQ);
    if (newInfobaseId) params.set("infobaseId", newInfobaseId);
    setSearchParams(params, { replace: true });
  };

  const handleKillClick = (session: SessionSnapshotEntry) => {
    setSelectedSession(session);
    setKillOpen(true);
  };

  const handleKillOpenChange = (open: boolean) => {
    setKillOpen(open);
    if (!open) setSelectedSession(null);
  };

  return (
    <TooltipProvider delayDuration={150}>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-2xl font-semibold tracking-tight">{t("sessions.title")}</h2>
            <p className="text-muted-foreground text-sm">{t("sessions.subtitle")}</p>
          </div>
          {data && (
            <span className="text-muted-foreground flex items-center gap-1 text-sm">
              {t("sessions.freshness.label")}{" "}
              <RelativeTime
                value={data.capturedAt}
                thresholdAmberSec={60}
                isError={failureCount >= 2}
              />
            </span>
          )}
        </div>

        {/* Error banner */}
        {isError && (
          <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
            <p className="font-medium">{t("sessions.errors.loadFailed")}</p>
            <Button
              variant="link"
              className="px-0"
              onClick={() => {
                void refetch().then((r) => {
                  if (r.isSuccess) toast.success(t("common.refresh"));
                });
              }}
            >
              {t("common.refresh")}
            </Button>
          </div>
        )}

        {/* Filter bar */}
        <div className="flex flex-wrap gap-3">
          <Input
            className="w-72"
            placeholder={t("sessions.filters.search")}
            value={q}
            onChange={(e) => setFilter({ q: e.target.value })}
          />
          <Select
            value={infobaseId || "_all"}
            onValueChange={(v) => setFilter({ infobaseId: v === "_all" ? "" : v })}
          >
            <SelectTrigger className="w-52">
              <SelectValue placeholder={t("sessions.filters.allInfobases")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="_all">{t("sessions.filters.allInfobases")}</SelectItem>
              {(infobasesData?.items ?? []).map((ib) => (
                <SelectItem key={ib.id} value={ib.id}>
                  {ib.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Table */}
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("sessions.table.tenant")}</TableHead>
                <TableHead>{t("sessions.table.infobase")}</TableHead>
                <TableHead className="w-28">{t("sessions.table.sessionId")}</TableHead>
                <TableHead className="w-28">{t("sessions.table.appId")}</TableHead>
                <TableHead>{t("sessions.table.user")}</TableHead>
                <TableHead className="w-40">{t("sessions.table.startedAt")}</TableHead>
                <TableHead className="w-24">{t("sessions.table.duration")}</TableHead>
                <TableHead className="w-28">{t("sessions.table.consumesLicense")}</TableHead>
                {isAdmin && <TableHead className="w-36">{t("sessions.table.action")}</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading
                ? Array.from({ length: 5 }).map((_, idx) => (
                    <TableRow key={`skeleton-${idx}`}>
                      {Array.from({ length: isAdmin ? 9 : 8 }).map((__, col) => (
                        <TableCell key={col}>
                          <Skeleton className="h-4 w-full" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                : filtered.length === 0
                  ? !isError && (
                      <TableRow>
                        <TableCell colSpan={isAdmin ? 9 : 8} className="py-12">
                          <div className="flex flex-col items-center justify-center gap-3 text-center">
                            <MonitorPlayIcon className="text-muted-foreground size-8" />
                            <div className="space-y-1">
                              <p className="font-medium">{t("sessions.empty.title")}</p>
                              <p className="text-muted-foreground text-sm">
                                {t("sessions.empty.hint")}
                              </p>
                            </div>
                          </div>
                        </TableCell>
                      </TableRow>
                    )
                  : filtered.map((row) => (
                      <SessionRow
                        key={row.sessionId}
                        row={row}
                        isAdmin={isAdmin}
                        onKill={handleKillClick}
                      />
                    ))}
            </TableBody>
          </Table>
        </div>

        <KillSessionDialog
          key={selectedSession?.sessionId ?? "new"}
          open={killOpen}
          onOpenChange={handleKillOpenChange}
          session={selectedSession}
        />
      </div>
    </TooltipProvider>
  );
}

interface SessionRowProps {
  row: SessionSnapshotEntry;
  isAdmin: boolean;
  onKill: (session: SessionSnapshotEntry) => void;
}

function SessionRow({ row, isAdmin, onKill }: SessionRowProps) {
  const { t } = useTranslation();
  const startedAtFormatted = format(new Date(row.startedAt), "dd.MM.yyyy HH:mm:ss", {
    locale: ru,
  });

  return (
    <TableRow>
      <TableCell className="font-medium">{row.tenantName}</TableCell>
      <TableCell>{row.infobaseName}</TableCell>
      <TableCell>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="cursor-help font-mono text-xs">
              {row.sessionId.replace(/-/g, "").slice(0, 8)}
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <span className="font-mono text-xs">{row.sessionId}</span>
          </TooltipContent>
        </Tooltip>
      </TableCell>
      <TableCell className="font-mono text-xs">{row.appId}</TableCell>
      <TableCell>{row.userName}</TableCell>
      <TableCell className="text-sm tabular-nums">{startedAtFormatted}</TableCell>
      <TableCell className="text-sm tabular-nums">{formatDuration(row.durationSeconds)}</TableCell>
      <TableCell>
        <StatusBadge variant={row.consumesLicense ? "success" : "neutral"}>
          {row.consumesLicense ? t("sessions.badges.consumesYes") : t("sessions.badges.consumesNo")}
        </StatusBadge>
      </TableCell>
      {isAdmin && (
        <TableCell>
          <Button size="sm" variant="outline" onClick={() => onKill(row)}>
            {t("sessions.kill.confirm")}
          </Button>
        </TableCell>
      )}
    </TableRow>
  );
}
