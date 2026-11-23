import { useApi, type PagedResult, type VisitRow } from '../api';
import FilterSelect from '../components/FilterSelect';
import LoadState from '../components/LoadState';
import Pager from '../components/Pager';
import { CITIES, PAGE_SIZE, SPECIES, formatCurrency, formatDate, formatNumber } from '../format';
import { usePageFilters } from '../searchParams';

export default function VisitsPage() {
  const { page, filters, setPage, setFilter } = usePageFilters(['species', 'city'] as const);
  const state = useApi<PagedResult<VisitRow>>('/api/visits', { page, pageSize: PAGE_SIZE, ...filters });

  return (
    <>
      <h1 className="h3">Past visits &amp; invoices</h1>
      {state.status === 'success' && (
        <p className="text-muted">
          {formatNumber(state.data.totalCount)} past visits &middot; page {state.data.page} of {state.data.totalPages}
        </p>
      )}

      <div className="row g-2 mb-3">
        <FilterSelect label="Species" allLabel="All species" options={SPECIES} value={filters.species} onChange={(v) => setFilter('species', v)} />
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
                  <th>Date</th><th>Owner</th><th>City</th><th>Pet</th><th>Species / breed</th>
                  <th>Vet</th><th>Clinic</th><th>Reason</th><th>Diagnosis</th><th className="text-end">Invoice</th>
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((v) => (
                  <tr key={v.visitId}>
                    <td>{formatDate(v.visitDate)}</td>
                    <td>{v.ownerName}<br /><small className="text-muted">{v.ownerEmail}</small></td>
                    <td>{v.city}</td>
                    <td>{v.petName}</td>
                    <td>{v.species}<br /><small className="text-muted">{v.breed}</small></td>
                    <td>{v.vetName}</td>
                    <td>{v.clinic}</td>
                    <td>{v.reason}</td>
                    <td>{v.diagnosis}</td>
                    <td className="text-end">{formatCurrency(v.cost)}</td>
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
