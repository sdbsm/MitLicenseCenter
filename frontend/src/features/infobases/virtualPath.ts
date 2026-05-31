// Виртуальный путь по умолчанию выводим из имени БД (обычно латиница, напр.
// acme_bp → /acme-bp). Оператор может переопределить в блоке «Дополнительно».
export function virtualPathFromDatabase(databaseName: string): string {
  const slug = databaseName
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug ? `/${slug}` : "";
}
