import { describe, it, expect } from "vitest";
import i18n from "@/i18n";
import { parseParams, defaultAppIds, resolveAppIds } from "../useSessionsPage";
import { appTypeLabel, isInteractiveAppId, INTERACTIVE_APP_IDS, KNOWN_APP_IDS } from "../appTypes";

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

describe("parseParams — consuming-тумблер «Только лицензионные» (MLC-167)", () => {
  it("чистый URL → consuming=true (ВКЛ по умолчанию)", () => {
    expect(parseParams(new URLSearchParams("")).consuming).toBe(true);
  });

  it("consuming=0 → выключен", () => {
    expect(parseParams(new URLSearchParams("consuming=0")).consuming).toBe(false);
  });

  it("любое значение кроме '0' → включён (только '0' выключает)", () => {
    expect(parseParams(new URLSearchParams("consuming=1")).consuming).toBe(true);
  });

  it("URL round-trip: ВКЛ → нет параметра; ВЫКЛ → consuming=0", () => {
    // ВКЛ — дефолтный, чистый URL (параметр не пишется).
    const onParams = new URLSearchParams();
    expect(onParams.toString()).toBe("");
    expect(parseParams(onParams).consuming).toBe(true);
    // ВЫКЛ — пишется consuming=0.
    const offParams = new URLSearchParams();
    offParams.set("consuming", "0");
    expect(parseParams(offParams).consuming).toBe(false);
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

describe("опции селекта типов — полный каталог (MLC-167)", () => {
  // Воспроизводим построение appTypeOptions из useSessionsPage: KNOWN_APP_IDS ∪ extras.
  function buildOptions(presentAppIds: string[]): string[] {
    const known = new Set<string>(KNOWN_APP_IDS);
    const extras = presentAppIds
      .filter((appId) => !known.has(appId))
      .sort((a, b) => a.localeCompare(b, "ru"));
    return [...KNOWN_APP_IDS, ...extras];
  }

  it("опции включают типы из каталога даже если их нет в снапшоте (напр. BackgroundJob)", () => {
    // Снапшот содержит только тонкий клиент — но BackgroundJob всё равно среди опций.
    const present = ["1CV8C"];
    const options = buildOptions(present);
    expect(options).toContain("BackgroundJob");
    expect(options).toContain("Debugger");
  });

  it("полный каталог присутствует целиком и в каноническом порядке", () => {
    expect(buildOptions([])).toEqual([...KNOWN_APP_IDS]);
  });

  it("незнакомый app-id из снапшота добавляется в конец (после каталога)", () => {
    const options = buildOptions(["1CV8C", "FutureClient"]);
    expect(options).toEqual([...KNOWN_APP_IDS, "FutureClient"]);
  });

  it("каталог не дублирует присутствующие типы", () => {
    const options = buildOptions(["BackgroundJob", "1CV8C"]);
    expect(options.filter((o) => o === "BackgroundJob")).toHaveLength(1);
  });
});

describe("фильтрация по consuming-тумблеру (логика filtered memo, MLC-167)", () => {
  // Воспроизводим режим тумблера из useSessionsPage.filtered.
  type Row = { appId: string; licenseStatus: "Consuming" | "NotConsuming" | "Pending" };
  const rows: Row[] = [
    { appId: "1CV8C", licenseStatus: "Consuming" },
    { appId: "1CV8C", licenseStatus: "NotConsuming" },
    { appId: "WebClient", licenseStatus: "Pending" },
    { appId: "BackgroundJob", licenseStatus: "Consuming" },
  ];

  function applyFilter(items: Row[], consuming: boolean, effectiveAppIds: string[]): Row[] {
    if (consuming) {
      return items.filter((r) => r.licenseStatus === "Consuming");
    }
    const set = new Set(effectiveAppIds);
    return items.filter((r) => set.has(r.appId));
  }

  it("тумблер ВКЛ → только Consuming, типы игнорируются (даже фоновый BackgroundJob)", () => {
    // effectiveAppIds намеренно пустой — в режиме consuming он не должен влиять.
    const result = applyFilter(rows, true, []);
    expect(result).toEqual([
      { appId: "1CV8C", licenseStatus: "Consuming" },
      { appId: "BackgroundJob", licenseStatus: "Consuming" },
    ]);
  });

  it("тумблер ВКЛ скрывает Pending и NotConsuming", () => {
    const statuses = applyFilter(rows, true, ["1CV8C", "WebClient", "BackgroundJob"]).map(
      (r) => r.licenseStatus
    );
    expect(statuses).not.toContain("Pending");
    expect(statuses).not.toContain("NotConsuming");
  });

  it("тумблер ВЫКЛ → действует фильтр типов, статус лицензии не учитывается", () => {
    const result = applyFilter(rows, false, ["1CV8C"]);
    expect(result).toEqual([
      { appId: "1CV8C", licenseStatus: "Consuming" },
      { appId: "1CV8C", licenseStatus: "NotConsuming" },
    ]);
  });
});
