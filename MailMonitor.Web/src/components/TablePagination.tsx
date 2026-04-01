interface TablePaginationProps {
  totalItems: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

export function paginateItems<T>(items: T[], page: number, pageSize: number): T[] {
  const safePageSize = Math.max(1, pageSize);
  const safePage = Math.max(1, page);
  const start = (safePage - 1) * safePageSize;
  return items.slice(start, start + safePageSize);
}

export function clampPage(page: number, totalItems: number, pageSize: number): number {
  const safePageSize = Math.max(1, pageSize);
  const totalPages = Math.max(1, Math.ceil(totalItems / safePageSize));
  return Math.min(Math.max(1, page), totalPages);
}

export function TablePagination({
  totalItems,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange
}: TablePaginationProps): JSX.Element {
  const safePage = Math.max(1, page);
  const safePageSize = Math.max(1, pageSize);
  const totalPages = Math.max(1, Math.ceil(totalItems / safePageSize));
  const start = totalItems === 0 ? 0 : (safePage - 1) * safePageSize + 1;
  const end = Math.min(totalItems, safePage * safePageSize);

  return (
    <div className="table-pagination">
      <div className="table-pagination-left">
        <span>Items por pagina</span>
        <select
          aria-label="Items por pagina"
          value={safePageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
        >
          <option value={5}>5</option>
          <option value={10}>10</option>
        </select>
      </div>

      <div className="table-pagination-right">
        <span>{`${start}-${end} de ${totalItems}`}</span>
        <button
          type="button"
          className="btn secondary"
          onClick={() => onPageChange(safePage - 1)}
          disabled={safePage <= 1}
        >
          Anterior
        </button>
        <span>{`Pagina ${safePage}/${totalPages}`}</span>
        <button
          type="button"
          className="btn secondary"
          onClick={() => onPageChange(safePage + 1)}
          disabled={safePage >= totalPages}
        >
          Siguiente
        </button>
      </div>
    </div>
  );
}
