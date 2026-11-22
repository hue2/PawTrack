import { useApi, type OwnerSummaryRow, type PagedResult } from '../api';
import FilterSelect from '../components/FilterSelect';
import LoadState from '../components/LoadState';
import Pager from '../components/Pager';
import { CITIES, PAGE_SIZE, formatCurrency, formatDate, formatNumber } from '../format';
import { usePageFilters } from '../searchParams';

export default function OwnersPage() {
  const { page, filters, setPage, setFilter } = usePageFilters(['city'] as const);
  const state = useApi<PagedResult<OwnerSummaryRow>>('/api/owners', { page, pageSize: PAGE_SIZE, ...filters });

  return (
    <>
      <h1 className="h3">Owners</h1>
      {state.status === 'success' && (
        <p className="text-muted">
          {formatNumber(state.data.totalCount)} owners &middot; page {state.data.page} of {state.data.totalPages}
        </p>
      )}

      <div className="row g-2 mb-3">
        <FilterSelect label="City" allLabel="All cities" options={CITIES} value={filters.city} onChange={(v) => setFilter('city', v)} />
      </div>

      <LoadState state={state} />
      {state.status === 'success' && (
        <>
          <Pager page={state.data.page} totalPages={state.data.totalPages} onChange={setPage} />
          <div className="table-responsive">
            <table className="table table-sm table-striped align-middle">
              <thead>
                <tr>
                  <th>Owner</th><th>City</th><th className="text-end">Pets</th><th className="text-end">Visits</th>
                  <th className="text-end">Total spend</th><th>Last visit</th>
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((o) => (
                  <tr key={o.userId}>
                    <td>{o.ownerName}</td>
                    <td>{o.city}</td>
                    <td className="text-end">{o.petCount}</td>
                    <td className="text-end">{o.visitCount}</td>
                    <td className="text-end">{formatCurrency(o.totalSpend)}</td>
                    <td>{formatDate(o.lastVisit)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={state.data.page} totalPages={state.data.totalPages} onChange={setPage} />
        </>
      )}
    </>
  );
}
