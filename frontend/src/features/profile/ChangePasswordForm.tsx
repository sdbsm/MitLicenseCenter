import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { useChangePassword } from "./useChangePassword";

// Бэкенд возвращает ошибки полей под этими ключами (PascalCase из DataAnnotations /
// IdentityError + camelCase fallback) — маппим их на поля формы.
const FIELD_BY_BACKEND_KEY: Record<string, "currentPassword" | "newPassword"> = {
  CurrentPassword: "currentPassword",
  NewPassword: "newPassword",
  currentPassword: "currentPassword",
  newPassword: "newPassword",
};

function buildSchema(t: (k: string) => string) {
  return z
    .object({
      currentPassword: z.string().min(1, t("profile.errors.currentRequired")),
      newPassword: z.string().min(12, t("profile.errors.newTooShort")),
      confirmPassword: z.string().min(1, t("profile.errors.confirmRequired")),
    })
    .superRefine((data, ctx) => {
      if (data.newPassword !== data.confirmPassword) {
        ctx.addIssue({
          code: "custom",
          path: ["confirmPassword"],
          message: t("profile.errors.confirmMismatch"),
        });
      }
      if (data.currentPassword && data.newPassword && data.currentPassword === data.newPassword) {
        ctx.addIssue({
          code: "custom",
          path: ["newPassword"],
          message: t("profile.errors.sameAsCurrent"),
        });
      }
    });
}

type FormValues = z.infer<ReturnType<typeof buildSchema>>;

interface ValidationProblemBody {
  errors?: Record<string, string[]>;
}

interface ChangePasswordFormProps {
  /** Вызывается после успешной смены (помимо тоста). Диалог топбара закрывается,
   *  экран форс-смены — инвалидирует /me, чтобы снять блокирующий гейт. */
  onSuccess?: () => void;
  /** Метка кнопки отправки (по умолчанию — «Сменить пароль»). */
  submitLabel?: string;
  /** Показывать ли кнопку «Сбросить» (на экране форс-смены не нужна). */
  showReset?: boolean;
  /** Колбэк кнопки «Отмена». Если не передан — кнопка не показывается
   *  (на блокирующем экране форс-смены выход заблокирован, «Отмена» не нужна). */
  onCancel?: () => void;
}

/**
 * Форма смены пароля. Переиспользуется диалогом user-меню в топбаре (MLC-084) и
 * блокирующим экраном форс-смены (MLC-059) — обе шлют текущий + новый пароль на
 * `/api/v1/auth/change-password`. Рендерит только `<form>`; обёртку (Dialog / экран)
 * задаёт вызывающая сторона.
 */
export function ChangePasswordForm({
  onSuccess,
  submitLabel,
  showReset = true,
  onCancel,
}: ChangePasswordFormProps) {
  const { t } = useTranslation();
  const changePassword = useChangePassword();

  const form = useForm<FormValues>({
    resolver: zodResolver(buildSchema(t)),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await changePassword.mutateAsync({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      });
      toast.success(t("profile.passwordChanged"));
      form.reset({ currentPassword: "", newPassword: "", confirmPassword: "" });
      onSuccess?.();
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const body = error.body as ValidationProblemBody | null;
        const fieldErrors = body?.errors ?? {};
        let mapped = false;
        for (const [key, messages] of Object.entries(fieldErrors)) {
          const field = FIELD_BY_BACKEND_KEY[key];
          const message = messages?.[0];
          if (field && message) {
            form.setError(field, { type: "server", message });
            mapped = true;
          }
        }
        if (!mapped) {
          toast.error(t("errors.generic"));
        }
        return;
      }
      toast.error(t("errors.generic"));
    }
  });

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate className="grid gap-4">
        <FormField
          control={form.control}
          name="currentPassword"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("profile.password.current")}</FormLabel>
              <FormControl>
                <Input type="password" autoComplete="current-password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="newPassword"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("profile.password.new")}</FormLabel>
              <FormControl>
                <Input type="password" autoComplete="new-password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="confirmPassword"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t("profile.password.confirm")}</FormLabel>
              <FormControl>
                <Input type="password" autoComplete="new-password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <p className="text-muted-foreground text-xs">{t("profile.password.policy")}</p>
        <div className="flex justify-end gap-2">
          {showReset && (
            <Button
              type="button"
              variant="ghost"
              disabled={changePassword.isPending}
              onClick={() => form.reset()}
            >
              {t("common.reset")}
            </Button>
          )}
          {onCancel && (
            <Button
              type="button"
              variant="ghost"
              disabled={changePassword.isPending}
              onClick={onCancel}
            >
              {t("common.cancel")}
            </Button>
          )}
          <Button type="submit" disabled={changePassword.isPending}>
            {changePassword.isPending
              ? t("profile.password.saving")
              : (submitLabel ?? t("profile.password.save"))}
          </Button>
        </div>
      </form>
    </Form>
  );
}
