import {
  KeyRoundIcon,
  MoreHorizontalIcon,
  PlusIcon,
  ShieldIcon,
  ShieldOffIcon,
  UserCheckIcon,
} from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/ui/StatusBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { AdminFormDialog } from "./AdminFormDialog";
import { DisableAdminDialog } from "./DisableAdminDialog";
import { EnableAdminDialog } from "./EnableAdminDialog";
import { GeneratedPasswordDialog } from "./GeneratedPasswordDialog";
import { ResetPasswordDialog } from "./ResetPasswordDialog";
import type { Admin } from "./types";
import { useAdmins } from "./useAdmins";

interface GeneratedPassword {
  userName: string;
  password: string;
}

export function AdminsPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch } = useAdmins();

  const [formOpen, setFormOpen] = useState(false);
  const [resetting, setResetting] = useState<Admin | null>(null);
  const [disabling, setDisabling] = useState<Admin | null>(null);
  const [enabling, setEnabling] = useState<Admin | null>(null);
  const [generated, setGenerated] = useState<GeneratedPassword | null>(null);

  const items = useMemo<Admin[]>(() => data?.items ?? [], [data]);

  const handlePasswordGenerated = (userName: string, password: string) => {
    setGenerated({ userName, password });
  };

  const renderRoles = (admin: Admin) =>
    admin.roles.map((role) => t(`admins.roles.${role}`, { defaultValue: role })).join(", ") || "—";

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("admins.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("admins.subtitle")}</p>
        </div>
        <Button onClick={() => setFormOpen(true)}>
          <PlusIcon className="size-4" />
          {t("admins.actions.add")}
        </Button>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("admins.errors.loadFailed")}</p>
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

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("admins.fields.userName")}</TableHead>
              <TableHead>{t("admins.fields.role")}</TableHead>
              <TableHead>{t("admins.fields.status")}</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading
              ? Array.from({ length: 3 }).map((_, idx) => (
                  <TableRow key={`skeleton-${idx}`}>
                    <TableCell>
                      <Skeleton className="h-4 w-40" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-24" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-5 w-20" />
                    </TableCell>
                    <TableCell />
                  </TableRow>
                ))
              : items.length === 0
                ? !isError && (
                    <TableRow>
                      <TableCell colSpan={4} className="py-12">
                        <div className="flex flex-col items-center justify-center gap-3 text-center">
                          <ShieldIcon className="text-muted-foreground size-8" />
                          <div className="space-y-1">
                            <p className="font-medium">{t("admins.empty.title")}</p>
                            <p className="text-muted-foreground text-sm">
                              {t("admins.empty.hint")}
                            </p>
                          </div>
                          <Button size="sm" onClick={() => setFormOpen(true)}>
                            <PlusIcon className="size-4" />
                            {t("admins.actions.add")}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                : items.map((admin) => (
                    <TableRow key={admin.id}>
                      <TableCell className="font-medium">{admin.userName}</TableCell>
                      <TableCell className="text-muted-foreground">{renderRoles(admin)}</TableCell>
                      <TableCell>
                        {admin.isActive ? (
                          <StatusBadge variant="success">{t("admins.status.active")}</StatusBadge>
                        ) : (
                          <StatusBadge variant="neutral">{t("admins.status.disabled")}</StatusBadge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="size-8">
                              <MoreHorizontalIcon className="size-4" />
                              <span className="sr-only">{t("common.details")}</span>
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem onSelect={() => setResetting(admin)}>
                              <KeyRoundIcon className="size-4" />
                              {t("admins.actions.resetPassword")}
                            </DropdownMenuItem>
                            {admin.isActive ? (
                              <DropdownMenuItem
                                variant="destructive"
                                onSelect={() => setDisabling(admin)}
                              >
                                <ShieldOffIcon className="size-4" />
                                {t("admins.actions.disable")}
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem onSelect={() => setEnabling(admin)}>
                                <UserCheckIcon className="size-4" />
                                {t("admins.actions.enable")}
                              </DropdownMenuItem>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))}
          </TableBody>
        </Table>
      </div>

      <AdminFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        onPasswordGenerated={handlePasswordGenerated}
      />
      <ResetPasswordDialog
        key={resetting?.id ?? "reset-none"}
        admin={resetting}
        open={resetting !== null}
        onOpenChange={(open) => {
          if (!open) setResetting(null);
        }}
        onPasswordGenerated={handlePasswordGenerated}
      />
      <DisableAdminDialog
        key={disabling?.id ?? "disable-none"}
        admin={disabling}
        open={disabling !== null}
        onOpenChange={(open) => {
          if (!open) setDisabling(null);
        }}
      />
      <EnableAdminDialog
        key={enabling?.id ?? "enable-none"}
        admin={enabling}
        open={enabling !== null}
        onOpenChange={(open) => {
          if (!open) setEnabling(null);
        }}
      />
      {generated && (
        <GeneratedPasswordDialog
          open={generated !== null}
          onOpenChange={(open) => {
            if (!open) setGenerated(null);
          }}
          userName={generated.userName}
          password={generated.password}
        />
      )}
    </div>
  );
}
