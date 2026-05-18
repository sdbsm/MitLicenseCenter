import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { useMe } from "@/features/auth/useAuth";
import { ApiError } from "@/lib/api";
import { useChangePassword } from "./useChangePassword";

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

export function ProfilePage() {
  const { t } = useTranslation();
  const { data: me } = useMe();
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
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("profile.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("profile.subtitle")}</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t("profile.account.title")}</CardTitle>
          <CardDescription>{t("profile.account.subtitle")}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 text-sm">
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground">{t("profile.account.userName")}</span>
            <span className="font-mono">{me?.userName ?? "—"}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground">{t("profile.account.roles")}</span>
            <span className="font-mono">{me?.roles?.join(", ") || "—"}</span>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("profile.password.title")}</CardTitle>
          <CardDescription>{t("profile.password.subtitle")}</CardDescription>
        </CardHeader>
        <Form {...form}>
          <form onSubmit={onSubmit} noValidate>
            <CardContent className="grid gap-4">
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
            </CardContent>
            <CardFooter className="justify-end gap-2">
              <Button
                type="button"
                variant="ghost"
                disabled={changePassword.isPending}
                onClick={() => form.reset()}
              >
                {t("common.reset")}
              </Button>
              <Button type="submit" disabled={changePassword.isPending}>
                {changePassword.isPending
                  ? t("profile.password.saving")
                  : t("profile.password.save")}
              </Button>
            </CardFooter>
          </form>
        </Form>
      </Card>
    </div>
  );
}
