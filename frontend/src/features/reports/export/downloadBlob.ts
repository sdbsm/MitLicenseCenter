/** Скачивание Blob в файл: создаём object URL, кликаем по временной ссылке и
 *  освобождаем URL. Готовых утилит скачивания в проекте нет (greenfield, MLC-051). */
export function downloadBlob(filename: string, blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
