/** Скачивание Blob в файл: object URL → клик по временной ссылке → освобождение URL.
 *  Локальная копия для записи быстродействия (паттерн `features/reports/export`, MLC-071);
 *  общей утилиты скачивания в проекте пока нет, обобщение — задача UI-холистик-трека. */
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
