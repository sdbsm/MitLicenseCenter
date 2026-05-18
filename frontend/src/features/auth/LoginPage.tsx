import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { z } from "zod";
import { ApiError } from "@/lib/api";
import { useLogin } from "./useAuth";

const schema = z.object({
  userName: z.string().trim().min(1),
  password: z.string().min(1),
});

type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const login = useLogin();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { userName: "", password: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const user = await login.mutateAsync(values);
      toast.success(t("auth.welcomeBack", { name: user.userName }));
      navigate("/", { replace: true });
    } catch (error) {
      const message =
        error instanceof ApiError && error.status === 401
          ? t("auth.invalidCredentials")
          : t("errors.generic");
      toast.error(message);
    }
  });

  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4">
      <div className="w-full max-w-sm rounded-xl border border-border bg-card p-8 shadow-sm">
        <div className="mb-6 space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t("auth.title")}</h1>
          <p className="text-sm text-muted-foreground">{t("auth.subtitle")}</p>
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
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
            {errors.userName && (
              <p className="text-xs text-status-danger">{t("auth.userNameRequired")}</p>
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
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
            {errors.password && (
              <p className="text-xs text-status-danger">{t("auth.passwordRequired")}</p>
            )}
          </div>

          <button
            type="submit"
            disabled={isSubmitting || login.isPending}
            className="w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground transition disabled:opacity-50"
          >
            {isSubmitting || login.isPending ? t("auth.signingIn") : t("auth.signIn")}
          </button>
        </form>
      </div>
    </div>
  );
}
