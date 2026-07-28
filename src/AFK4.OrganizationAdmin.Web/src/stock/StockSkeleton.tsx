import { Skeleton } from '../operatorPrimitives';

// Скелетон загрузки вкладок Склада: тихий каркас списка (шапка фильтров + строки), зеркалит
// финальную геометрию, чтобы контент не прыгал при появлении. Показывается через useDeferredFlag,
// поэтому быстрый ответ его не флешит.
export function StockSkeleton({
  sectionClass,
  label,
  rows = 6,
}: {
  sectionClass: string;
  label: string;
  rows?: number;
}) {
  return (
    <div className="stock-layout">
      <section className={sectionClass} role="status" aria-label={label} aria-busy="true">
        <Skeleton className="stock-skel-head" />
        <div className="stock-skel-list">
          {Array.from({ length: rows }).map((_, index) => (
            <Skeleton key={index} className="stock-row-skel" />
          ))}
        </div>
      </section>
    </div>
  );
}
