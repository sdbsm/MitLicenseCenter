import { useTranslation } from "react-i18next";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import { pageLinkRange } from "@/lib/pagination";

interface PaginationBarProps {
  /** Текущая страница (1-based, уже зажатая в [1, totalPages]). */
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  /** Идёт фоновое обновление страницы (показывает «Обновление…»). */
  isFetching?: boolean;
}

// Переиспользуемые контролы серверной пагинации над списками (MLC-015): сводка
// «from–to из total» + номера страниц. Ничего не рендерит, если всё помещается на одной странице.
export function PaginationBar({
  page,
  pageSize,
  total,
  onPageChange,
  isFetching = false,
}: PaginationBarProps) {
  const { t } = useTranslation();
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  if (total <= pageSize) {
    return null;
  }

  const go = (next: number) => {
    if (next < 1 || next > totalPages || next === page) return;
    onPageChange(next);
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-2">
      <p className="text-muted-foreground text-sm tabular-nums">
        {t("common.pagination.summary", {
          from: (page - 1) * pageSize + 1,
          to: Math.min(page * pageSize, total),
          total,
        })}
        {isFetching && <span className="ml-2">{t("common.pagination.refreshing")}</span>}
      </p>
      <Pagination className="mx-0 w-auto justify-end">
        <PaginationContent>
          <PaginationItem>
            <PaginationPrevious
              aria-disabled={page === 1}
              className={page === 1 ? "pointer-events-none opacity-50" : undefined}
              onClick={(e) => {
                e.preventDefault();
                go(page - 1);
              }}
            />
          </PaginationItem>
          {pageLinkRange(page, totalPages).map((p) => (
            <PaginationItem key={p}>
              <PaginationLink
                isActive={p === page}
                onClick={(e) => {
                  e.preventDefault();
                  go(p);
                }}
              >
                {p}
              </PaginationLink>
            </PaginationItem>
          ))}
          <PaginationItem>
            <PaginationNext
              aria-disabled={page === totalPages}
              className={page === totalPages ? "pointer-events-none opacity-50" : undefined}
              onClick={(e) => {
                e.preventDefault();
                go(page + 1);
              }}
            />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
    </div>
  );
}
