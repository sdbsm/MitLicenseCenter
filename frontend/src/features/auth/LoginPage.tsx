import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { z } from "zod";
import { ApiError, ApiNetworkError } from "@/lib/api";
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
    <div className="bg-background flex min-h-svh items-center justify-center px-4">
      <div className="border-border bg-card w-full max-w-sm rounded-xl border p-8 shadow-sm">
        <div className="mb-6 space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t("auth.title")}</h1>
          <p className="text-muted-foreground text-sm">{t("auth.subtitle")}</p>
        </div>

        <form onSubmit={onSubmit} className="space-y-4" noValidate>
          <div className="space-y-1.5">
            <label htmlFor="userName" className="text-sm font-medium">
              {t("auth.userName")}
            </label>
            <input
              id="userName"
              autoComplete="username"
              autoFocus
              {...register("userName")}
              className="border-input bg-background focus-visible:ring-ring w-full rounded-md border px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-2"
            />
            {errors.userName && (
              <p className="text-status-danger text-xs">{t("auth.userNameRequired")}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <label htmlFor="password" className="text-sm font-medium">
              {t("auth.password")}
            </label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              {...register("password")}
              className="border-input bg-background focus-visible:ring-ring w-full rounded-md border px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-2"
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

          <button
            type="submit"
            disabled={isSubmitting || login.isPending}
            className="bg-primary text-primary-foreground w-full rounded-md px-3 py-2 text-sm font-medium transition disabled:opacity-50"
          >
            {isSubmitting || login.isPending ? t("auth.signingIn") : t("auth.signIn")}
          </button>
        </form>
      </div>
    </div>
  );
}
