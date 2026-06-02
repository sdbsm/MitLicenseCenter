import { Skeleton } from "@/components/ui/skeleton";

/**
 * Короткий неблокирующий лоадер для границы <Suspense> вокруг лениво
 * подгружаемых страниц маршрутов (MLC-018). Текст не нужен — это мгновенный
 * скелетон на время загрузки чанка страницы, поэтому без i18n.
 */
export function PageFallback() {
  return (
    <div className="space-y-6" role="status" aria-busy="true">
      <div className="space-y-2">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </div>
      <Skeleton className="h-64 w-full" />
    </div>
  );
}
