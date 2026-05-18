import {
  Building2Icon,
  DatabaseIcon,
  GaugeIcon,
  GlobeIcon,
  MonitorPlayIcon,
  ScrollTextIcon,
  UserIcon,
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
import { NavLinkItem } from "./NavLinkItem";

export function Sidebar() {
  const { t } = useTranslation();

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
              <NavLinkItem to="/profile" icon={UserIcon} label={t("nav.profile")} />
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </SidebarRoot>
  );
}
