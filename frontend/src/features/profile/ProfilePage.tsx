import { useTranslation } from "react-i18next";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useMe } from "@/features/auth/useAuth";
import { ChangePasswordForm } from "./ChangePasswordForm";

export function ProfilePage() {
  const { t } = useTranslation();
  const { data: me } = useMe();

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
        <CardContent>
          <ChangePasswordForm />
        </CardContent>
      </Card>
    </div>
  );
}
