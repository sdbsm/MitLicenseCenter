import { QueryClientProvider } from "@tanstack/react-query";
import { useEffect } from "react";
import { I18nextProvider } from "react-i18next";
import { RouterProvider } from "react-router";
import { Toaster } from "sonner";
import { setUnauthorizedHandler } from "@/lib/api";
import { queryClient } from "@/lib/queryClient";
import { router } from "@/routes/router";
import i18n from "@/i18n";

export default function App() {
  useEffect(() => {
    setUnauthorizedHandler(() => {
      if (window.location.pathname !== "/login") {
        window.location.assign("/login");
      }
    });
    return () => setUnauthorizedHandler(null);
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <RouterProvider router={router} />
        <Toaster richColors position="top-right" closeButton />
      </I18nextProvider>
    </QueryClientProvider>
  );
}
