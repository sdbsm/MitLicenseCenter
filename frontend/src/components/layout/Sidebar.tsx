import {
  ActivityIcon,
  Building2Icon,
  DatabaseIcon,
  GaugeIcon,
  LineChartIcon,
  MonitorPlayIcon,
  ScrollTextIcon,
  SettingsIcon,
  UsersRoundIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  Sidebar as SidebarRoot,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
} from "@/components/ui/sidebar";
import { useMe } from "@/features/auth/useAuth";
import { NavLinkItem } from "./NavLinkItem";

// Группировка MLC-084 (UX-аудит §3.5): Обзор вне групп, далее Мониторинг /
// Управление / Система — итого 8 пунктов. Профиль живёт в топбаре, не здесь.
export function Sidebar() {
  const { t } = useTranslation();
  const { data: me } = useMe();
  const isAdmin = me?.roles?.includes("Admin") ?? false;

  return (
    <SidebarRoot collapsible="icon">
      <SidebarHeader className="border-sidebar-border border-b px-4 py-3">
        <div className="flex items-baseline gap-2 group-data-[collapsible=icon]:hidden">
          <span className="text-sm font-semibold tracking-tight">MitLicense</span>
          <span className="text-sidebar-foreground/60 text-xs">Center</span>
        </div>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              <NavLinkItem to="/" end icon={GaugeIcon} label={t("nav.dashboard")} />
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        <SidebarGroup>
          <SidebarGroupLabel>{t("nav.groups.monitoring")}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              <NavLinkItem to="/sessions" icon={MonitorPlayIcon} label={t("nav.sessions")} />
              <NavLinkItem to="/performance" icon={ActivityIcon} label={t("nav.performance")} />
              <NavLinkItem to="/reports" icon={LineChartIcon} label={t("nav.reports")} />
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        <SidebarGroup>
          <SidebarGroupLabel>{t("nav.groups.management")}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              <NavLinkItem to="/tenants" icon={Building2Icon} label={t("nav.tenants")} />
              <NavLinkItem to="/infobases" icon={DatabaseIcon} label={t("nav.infobases")} />
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        <SidebarGroup>
          <SidebarGroupLabel>{t("nav.groups.system")}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              <NavLinkItem to="/audit" icon={ScrollTextIcon} label={t("nav.audit")} />
              {isAdmin && <NavLinkItem to="/users" icon={UsersRoundIcon} label={t("nav.users")} />}
              {isAdmin && (
                <NavLinkItem to="/settings" icon={SettingsIcon} label={t("nav.settings")} />
              )}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </SidebarRoot>
  );
}
