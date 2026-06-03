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

interface AuditPaginationProps {
  currentPage: number;
  totalPages: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
}

/** Пагинация журнала аудита: сводка диапазона + переключатель страниц. */
export function AuditPagination({
  currentPage,
  totalPages,
  pageSize,
  total,
  onPageChange,
}: AuditPaginationProps) {
  const { t } = useTranslation();
  return (
    <div className="flex items-center justify-between gap-4">
      <p className="text-muted-foreground text-sm tabular-nums">
        {t("audit.pagination.summary", {
          from: (currentPage - 1) * pageSize + 1,
          to: Math.min(currentPage * pageSize, total),
          total,
        })}
      </p>
      <Pagination className="mx-0 w-auto justify-end">
        <PaginationContent>
          <PaginationItem>
            <PaginationPrevious
              aria-disabled={currentPage === 1}
              className={currentPage === 1 ? "pointer-events-none opacity-50" : undefined}
              onClick={(e) => {
                e.preventDefault();
                onPageChange(currentPage - 1);
              }}
            />
          </PaginationItem>
          {pageLinkRange(currentPage, totalPages).map((p) => (
            <PaginationItem key={p}>
              <PaginationLink
                isActive={p === currentPage}
                onClick={(e) => {
                  e.preventDefault();
                  onPageChange(p);
                }}
              >
                {p}
              </PaginationLink>
            </PaginationItem>
          ))}
          <PaginationItem>
            <PaginationNext
              aria-disabled={currentPage === totalPages}
              className={currentPage === totalPages ? "pointer-events-none opacity-50" : undefined}
              onClick={(e) => {
                e.preventDefault();
                onPageChange(currentPage + 1);
              }}
            />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
    </div>
  );
}
