import { QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "next-themes";
import { useEffect } from "react";
import { I18nextProvider } from "react-i18next";
import { RouterProvider } from "react-router";
import { Toaster } from "@/components/ui/sonner";
import { setUnauthorizedHandler } from "@/lib/api";
import { queryClient } from "@/lib/queryClient";
import { router } from "@/routes/router";
import i18n from "@/i18n";

export default function App() {
  useEffect(() => {
    setUnauthorizedHandler(() => {
      // Прежний window.location.assign делал полную перезагрузку, что заодно
      // сбрасывало in-memory кэш React Query. SPA-навигация через router-инстанс
      // страницу не перезагружает, поэтому кэш чистим явно (защищённые данные
      // протухшей сессии не должны пережить редирект на /login).
      queryClient.clear();
      if (router.state.location.pathname !== "/login") {
        void router.navigate("/login", { replace: true });
      }
    });
    return () => setUnauthorizedHandler(null);
  }, []);

  return (
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem storageKey="mlc-theme">
      <QueryClientProvider client={queryClient}>
        <I18nextProvider i18n={i18n}>
          <RouterProvider router={router} />
          <Toaster richColors position="top-right" closeButton />
        </I18nextProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
