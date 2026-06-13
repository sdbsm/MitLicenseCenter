import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
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
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { applyFieldErrors, matchConflictCode, toastFormSubmitError } from "@/lib/apiErrors";
import { USER_ROLES, type UserRole, type CreateUserInput } from "./types";
import { useCreateUser } from "./useUsers";

function buildSchema(t: (k: string) => string) {
  return z.object({
    userName: z
      .string()
      .trim()
      .min(1, t("users.errors.userNameRequired"))
      .max(256, t("users.errors.userNameTooLong")),
    role: z.enum(USER_ROLES),
  });
}

type FormValues = z.infer<ReturnType<typeof buildSchema>>;

interface UserFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  // Поднимает сгенерированный пароль наверх — страница показывает его в отдельном диалоге.
  onPasswordGenerated: (userName: string, password: string) => void;
}

export function UserFormDialog({ open, onOpenChange, onPasswordGenerated }: UserFormDialogProps) {
  const { t } = useTranslation();
  const create = useCreateUser();

  const form = useForm<FormValues>({
    resolver: zodResolver(buildSchema(t)),
    defaultValues: { userName: "", role: "Admin" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    const input: CreateUserInput = { userName: values.userName.trim(), role: values.role };
    try {
      const result = await create.mutateAsync(input);
      toast.success(t("users.toasts.created", { name: input.userName }));
      onOpenChange(false);
      onPasswordGenerated(result.userName, result.generatedPassword);
    } catch (error) {
      const mapped = matchConflictCode(error, {
        USER_USERNAME_DUPLICATE: {
          field: "userName",
          messageKey: "users.errors.userNameDuplicate",
        } as const,
      });
      if (mapped) {
        form.setError(mapped.field, { type: "server", message: t(mapped.messageKey) });
        return;
      }
      // UX-04 — 400 ValidationProblem → inline-ошибка поля.
      if (applyFieldErrors(error, form.setError, { UserName: "userName" })) return;
      toastFormSubmitError(error, t);
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("users.form.createTitle")}</DialogTitle>
          <DialogDescription>{t("users.form.subtitle")}</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={onSubmit} noValidate className="grid gap-4">
            <FormField
              control={form.control}
              name="userName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("users.fields.userName")}</FormLabel>
                  <FormControl>
                    <Input
                      autoFocus
                      autoComplete="off"
                      placeholder={t("users.form.userNamePlaceholder")}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="role"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("users.fields.role")}</FormLabel>
                  <FormControl>
                    <div role="radiogroup" className="grid gap-2">
                      {USER_ROLES.map((role: UserRole) => (
                        <label
                          key={role}
                          className="hover:bg-accent/50 flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2"
                        >
                          <input
                            type="radio"
                            name="role"
                            value={role}
                            checked={field.value === role}
                            onChange={() => field.onChange(role)}
                            className="mt-0.5 size-4 cursor-pointer"
                          />
                          <span className="grid gap-0.5">
                            <span className="text-sm font-medium">{t(`users.roles.${role}`)}</span>
                            <span className="text-muted-foreground text-xs">
                              {t(`users.roleHints.${role}`)}
                            </span>
                          </span>
                        </label>
                      ))}
                    </div>
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter className="gap-2">
              <Button
                type="button"
                variant="ghost"
                disabled={create.isPending}
                onClick={() => onOpenChange(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={create.isPending}>
                {create.isPending ? t("common.loading") : t("common.create")}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
