import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useHealth } from "@/features/health/useHealth";

interface AuthCardShellProps {
  /** Заголовок карточки. */
  title: string;
  /** Подзаголовок под заголовком. */
  subtitle: string;
  /** Ширина карточки — формы входа уже (`max-w-sm`), форс-смена шире (`max-w-md`). */
  widthClassName?: string;
  /** Тело карточки (форма). */
  children: ReactNode;
  /** Опц. футер над строкой версии (напр. кнопка «Выйти» на экране форс-смены). */
  footer?: ReactNode;
}

/**
 * Общая обёртка экранов аутентификации (вход / форс-смена пароля): центрирование на
 * всю высоту, карточка дизайн-системы, заголовок/подзаголовок и подвал с версией панели.
 *
 * Версию панели берём из анонимного `/api/v1/health` (`useHealth`) — он не требует
 * логина и отдаёт `version` ещё до аутентификации. При недоступном health строка версии
 * не рендерится. Логика версии живёт здесь в одном месте, чтобы не дублироваться по
 * экранам входа.
 */
export function AuthCardShell({
  title,
  subtitle,
  widthClassName = "max-w-sm",
  children,
  footer,
}: AuthCardShellProps) {
  const { t } = useTranslation();
  const { data: health } = useHealth();

  return (
    <div className="bg-background flex min-h-svh items-center justify-center px-4">
      <div
        className={`border-border bg-card w-full ${widthClassName} rounded-xl border p-8 shadow-sm`}
      >
        <div className="mb-6 space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
          <p className="text-muted-foreground text-sm">{subtitle}</p>
        </div>

        {children}

        {footer}

        {health?.version && (
          <p className="text-muted-foreground mt-4 border-t pt-4 text-center text-xs">
            {t("nav.version", { version: health.version })}
          </p>
        )}
      </div>
    </div>
  );
}
