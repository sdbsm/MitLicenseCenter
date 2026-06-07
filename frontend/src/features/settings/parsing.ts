// Чистые хелперы для пикера платформы и поля порта RAS на странице «Параметры».
// Вынесены отдельно из компонентов ради unit-тестов (parsing.test.ts).

// Версия платформы зашита в путь rac.exe: ...\1cv8\<version>\bin\rac.exe
// (RacPathDiscovery.Scan строит именно такие пути). Достаём <version> и проверяем
// его форматом «4 числовых сегмента» — длины НЕ фиксируем: у ранних сборок 1С 8.5
// одноцифровой build (8.5.1.1302), жёсткий \d{2}\.\d{4} их отрезал бы (гоча
// PlatformVersion в CLAUDE.md / ADR-3.3). Не похоже на стандартный путь → null
// (оператор задаёт версию вручную через escape-hatch).
const RAC_PATH_VERSION = /[\\/]1cv8[\\/]([^\\/]+)[\\/]bin[\\/]rac\.exe$/i;
const PLATFORM_VERSION = /^\d+\.\d+\.\d+\.\d+$/;

export function parsePlatformVersionFromRacPath(path: string): string | null {
  const match = RAC_PATH_VERSION.exec(path.trim());
  if (!match) {
    return null;
  }
  const version = match[1];
  return PLATFORM_VERSION.test(version) ? version : null;
}

// Конвенциональный порт RAS, если endpoint не задан/не парсится.
export const DEFAULT_RAS_PORT = 1545;

// Endpoint хранится в БД как host:port (kind HostPort, первый позиционный аргумент
// rac.exe — ADR-3.3). UI редактирует только порт (хост фиксирован localhost,
// single-node топология), поэтому достаём порт из части после последнего двоеточия.
export function parseRasPort(endpoint: string | null | undefined): number {
  if (!endpoint) {
    return DEFAULT_RAS_PORT;
  }
  const colon = endpoint.lastIndexOf(":");
  const tail = colon >= 0 ? endpoint.slice(colon + 1) : endpoint;
  const port = Number.parseInt(tail.trim(), 10);
  return Number.isInteger(port) ? port : DEFAULT_RAS_PORT;
}

// Собираем wire-формат host:port для записи в БД. Хост фиксирован localhost —
// бэкенд (WebinstArgs, RacExecutableRasClusterClient) по-прежнему читает host:port.
export function buildRasEndpoint(port: number | string): string {
  return `localhost:${port}`;
}
