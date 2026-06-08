import {
  ActivityIcon,
  Building2Icon,
  DatabaseIcon,
  GaugeIcon,
  GlobeIcon,
  LineChartIcon,
  MonitorPlayIcon,
  ScrollTextIcon,
  SettingsIcon,
  UserIcon,
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
          <SidebarGroupLabel>{t("nav.groups.operations")}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              <NavLinkItem to="/" end icon={GaugeIcon} label={t("nav.dashboard")} />
              <NavLinkItem to="/sessions" icon={MonitorPlayIcon} label={t("nav.sessions")} />
              <NavLinkItem to="/publications" icon={GlobeIcon} label={t("nav.publications")} />
              <NavLinkItem to="/reports" icon={LineChartIcon} label={t("nav.reports")} />
              <NavLinkItem to="/performance" icon={ActivityIcon} label={t("nav.performance")} />
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>

        <SidebarGroup>
          <SidebarGroupLabel>{t("nav.groups.configuration")}</SidebarGroupLabel>
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
              <NavLinkItem to="/profile" icon={UserIcon} label={t("nav.profile")} />
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </SidebarRoot>
  );
}
