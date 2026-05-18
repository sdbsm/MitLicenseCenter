import { ConstructionIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

interface ComingSoonPageProps {
  titleKey: string;
}

export function ComingSoonPage({ titleKey }: ComingSoonPageProps) {
  const { t } = useTranslation();

  return (
    <div className="border-border mx-auto flex max-w-xl flex-col items-center gap-3 rounded-xl border border-dashed p-10 text-center">
      <ConstructionIcon aria-hidden="true" className="text-muted-foreground size-10" />
      <h2 className="text-xl font-semibold tracking-tight">{t(titleKey)}</h2>
      <p className="text-muted-foreground text-sm">{t("common.comingSoon")}</p>
    </div>
  );
}
