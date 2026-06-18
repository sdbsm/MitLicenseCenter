import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError, ApiNetworkError } from "@/lib/api";
import { AuthCardShell } from "./AuthCardShell";
import { useLogin } from "./useAuth";

const schema = z.object({
  // UX-01: пробелы (в т.ч. внутренние) и прочие недопустимые символы режем до отправки —
  // набор сверен с ASP.NET Identity AllowedUserNameCharacters (латиница/цифры + -._@+).
  userName: z
    .string()
    .trim()
    .min(1)
    .regex(/^[a-zA-Z0-9\-._@+]+$/),
  password: z.string().min(1),
});

type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const login = useLogin();
  // UX-04 — inline-канал ошибки формы логина (неверные данные / нет связи) вместо
  // одного лишь тоста. Сбрасывается при каждой новой попытке отправки.
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { userName: "", password: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null);
    try {
      const user = await login.mutateAsync(values);
      toast.success(t("auth.welcomeBack", { name: user.userName }));
      navigate("/", { replace: true });
    } catch (error) {
      // 401 — неверные учётные данные; сетевой сбой — «нет связи» (живой errors.network);
      // прочее — generic. Inline обязателен (UX-04), тост оставлен вторичным сигналом.
      const message =
        error instanceof ApiError && error.status === 401
          ? t("auth.invalidCredentials")
          : error instanceof ApiNetworkError
            ? t("errors.network")
            : t("errors.generic");
      setSubmitError(message);
      toast.error(message);
    }
  });

  return (
    <AuthCardShell title={t("auth.title")} subtitle={t("auth.subtitle")}>
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="userName">{t("auth.userName")}</Label>
          <Input id="userName" autoComplete="username" autoFocus {...register("userName")} />
          {errors.userName && (
            <p className="text-status-danger text-xs">{t("auth.userNameRequired")}</p>
          )}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="password">{t("auth.password")}</Label>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            {...register("password")}
          />
          {errors.password && (
            <p className="text-status-danger text-xs">{t("auth.passwordRequired")}</p>
          )}
        </div>

        {submitError && (
          <p role="alert" className="text-status-danger text-sm">
            {submitError}
          </p>
        )}

        <Button type="submit" className="w-full" disabled={isSubmitting || login.isPending}>
          {isSubmitting || login.isPending ? t("auth.signingIn") : t("auth.signIn")}
        </Button>
      </form>
    </AuthCardShell>
  );
}
