interface PagerProps {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
}

export default function Pager({ page, totalPages, onChange }: PagerProps) {
  return (
    <nav className="d-flex gap-2 my-2">
      {page > 1 && (
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => onChange(page - 1)}>
          &laquo; Previous
        </button>
      )}
      {page < totalPages && (
        <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => onChange(page + 1)}>
          Next &raquo;
        </button>
      )}
    </nav>
  );
}
