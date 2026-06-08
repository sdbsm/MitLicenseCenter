import { format } from "date-fns";
import { ru } from "date-fns/locale";
import {
  KeyRoundIcon,
  MoreHorizontalIcon,
  PlusIcon,
  ShieldOffIcon,
  UserCheckIcon,
  UserCogIcon,
  UsersRoundIcon,
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
import { ChangeRoleDialog } from "./ChangeRoleDialog";
import { DisableUserDialog } from "./DisableUserDialog";
import { EnableUserDialog } from "./EnableUserDialog";
import { GeneratedPasswordDialog } from "./GeneratedPasswordDialog";
import { ResetPasswordDialog } from "./ResetPasswordDialog";
import type { User } from "./types";
import { UserFormDialog } from "./UserFormDialog";
import { useUsers } from "./useUsers";

interface GeneratedPassword {
  userName: string;
  password: string;
}

export function UsersPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError, refetch } = useUsers();

  const [formOpen, setFormOpen] = useState(false);
  const [resetting, setResetting] = useState<User | null>(null);
  const [disabling, setDisabling] = useState<User | null>(null);
  const [enabling, setEnabling] = useState<User | null>(null);
  const [changingRole, setChangingRole] = useState<User | null>(null);
  const [generated, setGenerated] = useState<GeneratedPassword | null>(null);

  const items = useMemo<User[]>(() => data?.items ?? [], [data]);

  const handlePasswordGenerated = (userName: string, password: string) => {
    setGenerated({ userName, password });
  };

  const renderRoles = (user: User) =>
    user.roles.map((role) => t(`users.roles.${role}`, { defaultValue: role })).join(", ") || "—";

  const renderLastLogin = (user: User) =>
    user.lastLoginAt ? format(new Date(user.lastLoginAt), "dd.MM.yyyy HH:mm", { locale: ru }) : "—";

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-2xl font-semibold tracking-tight">{t("users.title")}</h2>
          <p className="text-muted-foreground text-sm">{t("users.subtitle")}</p>
        </div>
        <Button onClick={() => setFormOpen(true)}>
          <PlusIcon className="size-4" />
          {t("users.actions.add")}
        </Button>
      </div>

      {isError && (
        <div className="border-destructive/40 bg-destructive/5 rounded-md border p-4 text-sm">
          <p className="font-medium">{t("users.errors.loadFailed")}</p>
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
              <TableHead>{t("users.fields.userName")}</TableHead>
              <TableHead>{t("users.fields.role")}</TableHead>
              <TableHead>{t("users.fields.status")}</TableHead>
              <TableHead>{t("users.fields.lastLogin")}</TableHead>
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
                    <TableCell>
                      <Skeleton className="h-4 w-32" />
                    </TableCell>
                    <TableCell />
                  </TableRow>
                ))
              : items.length === 0
                ? !isError && (
                    <TableRow>
                      <TableCell colSpan={5} className="py-12">
                        <div className="flex flex-col items-center justify-center gap-3 text-center">
                          <UsersRoundIcon className="text-muted-foreground size-8" />
                          <div className="space-y-1">
                            <p className="font-medium">{t("users.empty.title")}</p>
                            <p className="text-muted-foreground text-sm">{t("users.empty.hint")}</p>
                          </div>
                          <Button size="sm" onClick={() => setFormOpen(true)}>
                            <PlusIcon className="size-4" />
                            {t("users.actions.add")}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  )
                : items.map((user) => (
                    <TableRow key={user.id}>
                      <TableCell className="font-medium">{user.userName}</TableCell>
                      <TableCell className="text-muted-foreground">{renderRoles(user)}</TableCell>
                      <TableCell>
                        {user.isActive ? (
                          <StatusBadge variant="success">{t("users.status.active")}</StatusBadge>
                        ) : (
                          <StatusBadge variant="neutral">{t("users.status.disabled")}</StatusBadge>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground tabular-nums">
                        {renderLastLogin(user)}
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
                            <DropdownMenuItem onSelect={() => setResetting(user)}>
                              <KeyRoundIcon className="size-4" />
                              {t("users.actions.resetPassword")}
                            </DropdownMenuItem>
                            <DropdownMenuItem onSelect={() => setChangingRole(user)}>
                              <UserCogIcon className="size-4" />
                              {t("users.actions.changeRole")}
                            </DropdownMenuItem>
                            {user.isActive ? (
                              <DropdownMenuItem
                                variant="destructive"
                                onSelect={() => setDisabling(user)}
                              >
                                <ShieldOffIcon className="size-4" />
                                {t("users.actions.disable")}
                              </DropdownMenuItem>
                            ) : (
                              <DropdownMenuItem onSelect={() => setEnabling(user)}>
                                <UserCheckIcon className="size-4" />
                                {t("users.actions.enable")}
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

      <UserFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        onPasswordGenerated={handlePasswordGenerated}
      />
      <ResetPasswordDialog
        key={resetting?.id ?? "reset-none"}
        user={resetting}
        open={resetting !== null}
        onOpenChange={(open) => {
          if (!open) setResetting(null);
        }}
        onPasswordGenerated={handlePasswordGenerated}
      />
      <DisableUserDialog
        key={disabling?.id ?? "disable-none"}
        user={disabling}
        open={disabling !== null}
        onOpenChange={(open) => {
          if (!open) setDisabling(null);
        }}
      />
      <EnableUserDialog
        key={enabling?.id ?? "enable-none"}
        user={enabling}
        open={enabling !== null}
        onOpenChange={(open) => {
          if (!open) setEnabling(null);
        }}
      />
      <ChangeRoleDialog
        key={changingRole?.id ?? "role-none"}
        user={changingRole}
        open={changingRole !== null}
        onOpenChange={(open) => {
          if (!open) setChangingRole(null);
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
