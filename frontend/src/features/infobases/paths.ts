// Хелперы для предзаполнения путей публикации из имени базы данных.

// Виртуальный путь выводим из имени БД (обычно латиница, напр. acme_bp → /acme-bp).
// Оператор может переопределить в блоке «Дополнительно».
export function virtualPathFromDatabase(databaseName: string): string {
  const slug = databaseName
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug ? `/${slug}` : "";
}

// Физический путь публикации = {корень}\{имя базы}. Корень — настройка
// IIS.DefaultVrdRoot (по умолчанию C:\inetpub\wwwroot). Сегмент сайта IIS в путь
// не входит — папка приложения называется по имени базы.
export function physicalPathFromDatabase(root: string, databaseName: string): string {
  const cleanRoot = root.replace(/[\\/]+$/, "");
  const folder = databaseName.trim();
  return folder ? `${cleanRoot}\\${folder}` : "";
}
