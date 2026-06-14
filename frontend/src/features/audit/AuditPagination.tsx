import { useState } from "react";
import { ChevronsLeft, ChevronsRight } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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

/** Пагинация журнала аудита: сводка диапазона + переключатель страниц с переходом на N. */
export function AuditPagination({
  currentPage,
  totalPages,
  pageSize,
  total,
  onPageChange,
}: AuditPaginationProps) {
  const { t } = useTranslation();
  const isFirst = currentPage === 1;
  const isLast = currentPage === totalPages;

  // Локальный буфер «перейти к странице»; синхронизируется с текущей страницей
  // паттерном «правка state во время рендера» (без эффекта) — при смене страницы
  // извне (клик по ссылке/кнопке, shareable-link) поле подхватывает новое значение.
  const [jumpDraft, setJumpDraft] = useState(String(currentPage));
  const [syncedPage, setSyncedPage] = useState(currentPage);
  if (syncedPage !== currentPage) {
    setSyncedPage(currentPage);
    setJumpDraft(String(currentPage));
  }

  const commitJump = () => {
    const parsed = Number(jumpDraft);
    if (!Number.isFinite(parsed)) {
      setJumpDraft(String(currentPage));
      return;
    }
    // Clamp в [1, totalPages] — вне диапазона не роняет, а прижимает к краю.
    const clamped = Math.min(totalPages, Math.max(1, Math.floor(parsed)));
    setJumpDraft(String(clamped));
    if (clamped !== currentPage) onPageChange(clamped);
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <p className="text-muted-foreground text-sm tabular-nums">
        {t("audit.pagination.summary", {
          from: (currentPage - 1) * pageSize + 1,
          to: Math.min(currentPage * pageSize, total),
          total,
        })}
      </p>
      <div className="flex flex-wrap items-center justify-end gap-2">
        <div className="flex items-center gap-1.5 text-sm">
          <label htmlFor="audit-jump" className="text-muted-foreground whitespace-nowrap">
            {t("audit.pagination.goToPage")}
          </label>
          <Input
            id="audit-jump"
            type="number"
            min={1}
            max={totalPages}
            inputMode="numeric"
            className="h-9 w-20 tabular-nums"
            value={jumpDraft}
            onChange={(e) => setJumpDraft(e.target.value)}
            onBlur={commitJump}
            onKeyDown={(e) => {
              if (e.key === "Enter") commitJump();
            }}
            aria-label={t("audit.pagination.goToPage")}
          />
          <span className="text-muted-foreground whitespace-nowrap tabular-nums">
            {t("audit.pagination.ofTotal", { total: totalPages })}
          </span>
        </div>
        <Pagination className="mx-0 w-auto justify-end">
          <PaginationContent>
            <PaginationItem>
              <Button
                variant="ghost"
                size="icon"
                aria-label={t("audit.pagination.first")}
                aria-disabled={isFirst}
                disabled={isFirst}
                onClick={() => onPageChange(1)}
              >
                <ChevronsLeft className="size-4" />
              </Button>
            </PaginationItem>
            <PaginationItem>
              <PaginationPrevious
                aria-disabled={isFirst}
                className={isFirst ? "pointer-events-none opacity-50" : undefined}
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
                aria-disabled={isLast}
                className={isLast ? "pointer-events-none opacity-50" : undefined}
                onClick={(e) => {
                  e.preventDefault();
                  onPageChange(currentPage + 1);
                }}
              />
            </PaginationItem>
            <PaginationItem>
              <Button
                variant="ghost"
                size="icon"
                aria-label={t("audit.pagination.last")}
                aria-disabled={isLast}
                disabled={isLast}
                onClick={() => onPageChange(totalPages)}
              >
                <ChevronsRight className="size-4" />
              </Button>
            </PaginationItem>
          </PaginationContent>
        </Pagination>
      </div>
    </div>
  );
}
