import { describe, it, expect } from "vitest";
import i18n from "@/i18n";
import { parseParams, defaultAppIds, resolveAppIds } from "../useSessionsPage";
import { appTypeLabel, isInteractiveAppId, INTERACTIVE_APP_IDS } from "../appTypes";

/**
 * MLC-165: фильтр по типам сеансов (app-id). Покрываем дефолт-поведение (фоновые скрыты),
 * семантику пустого выбора, URL round-trip и человеческие подписи.
 */

describe("parseParams — appIds (MLC-165)", () => {
  it("отсутствие параметра appIds → null (дефолт: интерактивные)", () => {
    const { appIds } = parseParams(new URLSearchParams(""));
    expect(appIds).toBeNull();
  });

  it("явный appIds=1CV8C,1CV8 → массив в порядке URL", () => {
    const { appIds } = parseParams(new URLSearchParams("appIds=1CV8C,1CV8"));
    expect(appIds).toEqual(["1CV8C", "1CV8"]);
  });

  it("пустой явный appIds= → пустой массив (валидное «показать пусто»)", () => {
    const { appIds } = parseParams(new URLSearchParams("appIds="));
    expect(appIds).toEqual([]);
  });

  it("обрезает пробелы и пустые сегменты", () => {
    const { appIds } = parseParams(new URLSearchParams("appIds=1CV8C, ,1CV8 ,"));
    expect(appIds).toEqual(["1CV8C", "1CV8"]);
  });

  it("сохраняет q/infobaseId без изменений", () => {
    const p = parseParams(new URLSearchParams("q=acme&infobaseId=ib-1&appIds=1CV8"));
    expect(p.q).toBe("acme");
    expect(p.infobaseId).toBe("ib-1");
    expect(p.appIds).toEqual(["1CV8"]);
  });
});

describe("defaultAppIds — дефолт скрывает фоновые (MLC-165)", () => {
  it("оставляет только интерактивные типы из присутствующих", () => {
    const present = ["1CV8C", "BackgroundJob", "WebClient", "JobScheduler", "Designer"];
    expect(defaultAppIds(present)).toEqual(["1CV8C", "WebClient", "Designer"]);
  });

  it("дефолт = пересечение интерактивного набора с присутствующими (не показывает отсутствующие)", () => {
    // В снапшоте только 1CV8C и фоновые → дефолт = [1CV8C], а не весь INTERACTIVE_APP_IDS.
    const present = ["1CV8C", "BackgroundJob", "SystemBackgroundJob"];
    expect(defaultAppIds(present)).toEqual(["1CV8C"]);
  });

  it("снапшот без интерактивных → пустой дефолт (нет ложных опций)", () => {
    expect(defaultAppIds(["BackgroundJob", "SrvrConsole"])).toEqual([]);
  });

  it("все интерактивные типы признаются интерактивными", () => {
    for (const id of INTERACTIVE_APP_IDS) {
      expect(isInteractiveAppId(id)).toBe(true);
    }
  });

  it("фоновые/служебные типы — не интерактивные", () => {
    for (const id of ["BackgroundJob", "SystemBackgroundJob", "JobScheduler", "SrvrConsole"]) {
      expect(isInteractiveAppId(id)).toBe(false);
    }
  });
});

describe("resolveAppIds — эффективный выбор (MLC-165)", () => {
  const present = ["1CV8C", "BackgroundJob", "WebClient"];

  it("null (параметр отсутствует) → дефолт-интерактивные ∩ present", () => {
    expect(resolveAppIds(null, present)).toEqual(["1CV8C", "WebClient"]);
  });

  it("явный выбор используется как есть (в т.ч. фоновые)", () => {
    expect(resolveAppIds(["BackgroundJob"], present)).toEqual(["BackgroundJob"]);
  });

  it("явный пустой выбор → пусто (оператор снял все)", () => {
    expect(resolveAppIds([], present)).toEqual([]);
  });
});

describe("фильтрация строк по effectiveAppIds (логика filtered memo)", () => {
  // Воспроизводим фильтр из useSessionsPage: rows.filter(r => set.has(r.appId)).
  const rows = [
    { appId: "1CV8C" },
    { appId: "WebClient" },
    { appId: "BackgroundJob" },
    { appId: "SystemBackgroundJob" },
  ];
  const present = ["1CV8C", "WebClient", "BackgroundJob", "SystemBackgroundJob"];

  function filterByAppIds<T extends { appId: string }>(items: T[], appIds: string[]): T[] {
    const set = new Set(appIds);
    return items.filter((r) => set.has(r.appId));
  }

  it("дефолт (null) скрывает фоновые сеансы", () => {
    const eff = resolveAppIds(null, present);
    const result = filterByAppIds(rows, eff).map((r) => r.appId);
    expect(result).toEqual(["1CV8C", "WebClient"]);
  });

  it("явный выбор одного типа → только он", () => {
    const eff = resolveAppIds(["BackgroundJob"], present);
    const result = filterByAppIds(rows, eff).map((r) => r.appId);
    expect(result).toEqual(["BackgroundJob"]);
  });

  it("явный пустой выбор → пустой результат", () => {
    const eff = resolveAppIds([], present);
    expect(filterByAppIds(rows, eff)).toEqual([]);
  });
});

describe("appTypeLabel — человеческие имена типов (MLC-165)", () => {
  it("известные app-id → русские подписи", () => {
    expect(appTypeLabel(i18n.t, "1CV8")).toBe("Толстый клиент");
    expect(appTypeLabel(i18n.t, "1CV8C")).toBe("Тонкий клиент");
    expect(appTypeLabel(i18n.t, "WebClient")).toBe("Веб-клиент");
    expect(appTypeLabel(i18n.t, "Designer")).toBe("Конфигуратор");
    expect(appTypeLabel(i18n.t, "BackgroundJob")).toBe("Фоновое задание");
    expect(appTypeLabel(i18n.t, "SrvrConsole")).toBe("Консоль кластера");
  });

  it("неизвестный app-id показывается как есть (не падает)", () => {
    expect(appTypeLabel(i18n.t, "SomeFutureClient")).toBe("SomeFutureClient");
  });
});

describe("URL round-trip appIds (MLC-165)", () => {
  it("выбор → CSV в URL → обратно тот же массив", () => {
    const selected = ["1CV8C", "1CV8"];
    const params = new URLSearchParams();
    params.set("appIds", selected.join(","));
    expect(params.toString()).toContain("appIds=1CV8C%2C1CV8");
    const { appIds } = parseParams(new URLSearchParams(params.toString()));
    expect(appIds).toEqual(selected);
  });

  it("пустой выбор → appIds= в URL → пустой массив (а не null)", () => {
    const params = new URLSearchParams();
    params.set("appIds", [].join(","));
    const { appIds } = parseParams(new URLSearchParams(params.toString()));
    expect(appIds).toEqual([]);
  });
});
