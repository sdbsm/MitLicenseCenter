import type { ComponentType, SVGProps } from "react";
import { Link, useMatch, useResolvedPath } from "react-router";
import { SidebarMenuButton, SidebarMenuItem } from "@/components/ui/sidebar";

type IconComponent = ComponentType<SVGProps<SVGSVGElement> & { className?: string }>;

interface NavLinkItemProps {
  to: string;
  icon: IconComponent;
  label: string;
  end?: boolean;
}

export function NavLinkItem({ to, icon: Icon, label, end }: NavLinkItemProps) {
  const resolved = useResolvedPath(to);
  const match = useMatch({ path: resolved.pathname, end: end ?? false });
  const isActive = match !== null;

  return (
    <SidebarMenuItem>
      <SidebarMenuButton asChild isActive={isActive} tooltip={label}>
        <Link to={to}>
          <Icon aria-hidden="true" />
          <span>{label}</span>
        </Link>
      </SidebarMenuButton>
    </SidebarMenuItem>
  );
}
