import { Suspense } from "react";
import { Outlet } from "react-router";
import { PageFallback } from "@/components/PageFallback";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

export function AppShell() {
  return (
    <SidebarProvider>
      <Sidebar />
      <SidebarInset className="flex min-h-svh flex-col">
        <Topbar />
        <main className="bg-background flex-1 overflow-y-auto px-6 py-6">
          {/* Общая граница Suspense для лениво подгружаемых страниц маршрутов
              (MLC-018): каркас остаётся на месте, пока грузится чанк страницы. */}
          <Suspense fallback={<PageFallback />}>
            <Outlet />
          </Suspense>
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
