/**
 * Форматирование размера в байтах → КБ/МБ/ГБ (база 1024) — единая конвенция
 * приложения (MLC-185d). Переиспользуется размером файла бэкапа (formatBackupSize)
 * и текущим размером базы в списке/карточке «Базы». Округление: ГБ/МБ — один знак
 * после запятой, КБ — целое (мелкие значения).
 */
export function formatBytes(bytes: number): string {
  const gb = bytes / 1024 ** 3;
  if (gb >= 1) return `${gb.toFixed(1)} ГБ`;
  const mb = bytes / 1024 ** 2;
  if (mb >= 1) return `${mb.toFixed(1)} МБ`;
  return `${Math.round(bytes / 1024)} КБ`;
}
