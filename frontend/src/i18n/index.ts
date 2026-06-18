import i18n from "i18next";
import { initReactI18next } from "react-i18next";

// Словарь UI-текстов разнесён по per-feature файлам в ./ru/ (по одному файлу на
// каждый top-level ключ). Здесь они собираются обратно в ОДИН объект и один
// namespace "translation" — структура итогового словаря идентична прежнему
// плоскому ru.json (MLC-027). Каждый файл оборачивает свой top-level ключ
// (напр. ru/common.json = { "common": { … } }), поэтому сборка — Object.assign.
import audit from "./ru/audit.json";
import auth from "./ru/auth.json";
import backups from "./ru/backups.json";
import common from "./ru/common.json";
import dashboard from "./ru/dashboard.json";
import design from "./ru/design.json";
import discovery from "./ru/discovery.json";
import errors from "./ru/errors.json";
import infobases from "./ru/infobases.json";
import nav from "./ru/nav.json";
import performance from "./ru/performance.json";
import profile from "./ru/profile.json";
import publications from "./ru/publications.json";
import reports from "./ru/reports.json";
import server from "./ru/server.json";
import sessions from "./ru/sessions.json";
import settings from "./ru/settings.json";
import table from "./ru/table.json";
import tenants from "./ru/tenants.json";
import theme from "./ru/theme.json";
import updates from "./ru/updates.json";
import users from "./ru/users.json";

export const ru = {
  ...common,
  ...nav,
  ...theme,
  ...table,
  ...auth,
  ...dashboard,
  ...design,
  ...profile,
  ...tenants,
  ...users,
  ...infobases,
  ...backups,
  ...publications,
  ...reports,
  ...server,
  ...performance,
  ...audit,
  ...sessions,
  ...settings,
  ...discovery,
  ...updates,
  ...errors,
};

void i18n.use(initReactI18next).init({
  resources: {
    ru: { translation: ru },
  },
  lng: "ru",
  fallbackLng: "ru",
  interpolation: {
    escapeValue: false,
  },
  returnNull: false,
});

export default i18n;
