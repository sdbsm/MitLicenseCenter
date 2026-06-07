import { describe, it, expect } from "vitest";
import {
  parsePlatformVersionFromRacPath,
  parseRasPort,
  buildRasEndpoint,
  DEFAULT_RAS_PORT,
} from "../parsing";

describe("parsePlatformVersionFromRacPath", () => {
  it("достаёт версию из стандартного пути rac.exe (8.3)", () => {
    expect(
      parsePlatformVersionFromRacPath("C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\rac.exe")
    ).toBe("8.3.23.1865");
  });

  it("работает с одноцифровым build 1С 8.5 (длины не фиксируем)", () => {
    expect(
      parsePlatformVersionFromRacPath("C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\rac.exe")
    ).toBe("8.5.1.1302");
  });

  it("регистронезависим и принимает прямые слэши", () => {
    expect(parsePlatformVersionFromRacPath("D:/1CV8/8.3.24.1548/BIN/RAC.EXE")).toBe("8.3.24.1548");
  });

  it("обрезает пробелы по краям", () => {
    expect(
      parsePlatformVersionFromRacPath("  C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\rac.exe  ")
    ).toBe("8.3.23.1865");
  });

  it("возвращает null для пути без сегмента 1cv8\\<version>\\bin", () => {
    expect(parsePlatformVersionFromRacPath("C:\\custom\\rac.exe")).toBeNull();
    expect(parsePlatformVersionFromRacPath("C:\\Program Files\\1cv8\\common\\rac.exe")).toBeNull();
  });

  it("возвращает null, если сегмент версии не 4 числовых поля", () => {
    expect(parsePlatformVersionFromRacPath("C:\\1cv8\\8.3.23\\bin\\rac.exe")).toBeNull();
    expect(parsePlatformVersionFromRacPath("C:\\1cv8\\8.3.23.1865-rc\\bin\\rac.exe")).toBeNull();
  });

  it("возвращает null для пустой строки", () => {
    expect(parsePlatformVersionFromRacPath("")).toBeNull();
  });
});

describe("parseRasPort", () => {
  it("достаёт порт из localhost:1545", () => {
    expect(parseRasPort("localhost:1545")).toBe(1545);
  });

  it("достаёт порт из произвольного host:port", () => {
    expect(parseRasPort("ras.local:1600")).toBe(1600);
  });

  it("дефолтит на 1545 для пустого/нулевого значения", () => {
    expect(parseRasPort(null)).toBe(DEFAULT_RAS_PORT);
    expect(parseRasPort(undefined)).toBe(DEFAULT_RAS_PORT);
    expect(parseRasPort("")).toBe(DEFAULT_RAS_PORT);
  });

  it("дефолтит на 1545 для значения без числового порта", () => {
    expect(parseRasPort("localhost:")).toBe(DEFAULT_RAS_PORT);
    expect(parseRasPort("localhost")).toBe(DEFAULT_RAS_PORT);
  });
});

describe("buildRasEndpoint", () => {
  it("собирает localhost:<port> из числа", () => {
    expect(buildRasEndpoint(1600)).toBe("localhost:1600");
  });

  it("собирает localhost:<port> из строки", () => {
    expect(buildRasEndpoint("1545")).toBe("localhost:1545");
  });
});
