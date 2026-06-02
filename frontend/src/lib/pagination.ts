/**
 * Возвращает номера страниц для контролов пагинации: все страницы, если их немного,
 * иначе — скользящее окно вокруг текущей, прижатое к краям [1, totalPages].
 */
export function pageLinkRange(current: number, totalPages: number, maxLinks = 7): number[] {
  if (totalPages <= maxLinks) {
    return Array.from({ length: totalPages }, (_, idx) => idx + 1);
  }
  const half = Math.floor(maxLinks / 2);
  const end = Math.min(totalPages, Math.max(current + half, maxLinks));
  const start = Math.max(1, end - maxLinks + 1);
  return Array.from({ length: end - start + 1 }, (_, idx) => start + idx);
}
