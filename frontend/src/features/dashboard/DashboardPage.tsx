import { useTranslation } from "react-i18next";

export function DashboardPage() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h2 className="text-2xl font-semibold tracking-tight">{t("dashboard.title")}</h2>
        <p className="text-muted-foreground text-sm">{t("dashboard.subtitle")}</p>
      </div>

      <div className="border-border text-muted-foreground rounded-xl border border-dashed p-6 text-sm">
        {t("dashboard.stub")}
      </div>
    </div>
  );
}
