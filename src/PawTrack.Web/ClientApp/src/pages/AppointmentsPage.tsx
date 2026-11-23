import { useApi, type AppointmentRow, type PagedResult } from '../api';
import LoadState from '../components/LoadState';
import Pager from '../components/Pager';
import { PAGE_SIZE, formatDate, formatDateTime, formatNumber } from '../format';
import { usePageFilters } from '../searchParams';

const TABS = [
  { status: '', label: 'Upcoming', description: 'Confirmed appointments, soonest first.' },
  { status: 'pending', label: 'Pending', description: 'Requested appointments awaiting confirmation, soonest first.' },
];

export default function AppointmentsPage() {
  const { page, filters, setPage, setFilter } = usePageFilters(['status'] as const);
  const isPending = filters.status === 'pending';
  const tab = TABS.find((t) => t.status === filters.status) ?? TABS[0];
  const state = useApi<PagedResult<AppointmentRow>>('/api/appointments', { page, pageSize: PAGE_SIZE, ...filters });

  return (
    <>
      <h1 className="h3">Appointments</h1>

      <ul className="nav nav-tabs mb-3">
        {TABS.map((t) => (
          <li key={t.label} className="nav-item">
            <button
              type="button"
              className={`nav-link ${t === tab ? 'active' : ''}`}
              aria-current={t === tab ? 'page' : undefined}
              onClick={() => setFilter('status', t.status)}
            >
              {t.label}
            </button>
          </li>
        ))}
      </ul>

      <p className="text-muted">
        {tab.description}
        {state.status === 'success' && (
          <> {formatNumber(state.data.totalCount)} appointments &middot; page {state.data.page} of {Math.max(state.data.totalPages, 1)}</>
        )}
      </p>

      <LoadState state={state} />
      {state.status === 'success' && (
        <>
          <Pager page={state.data.page} totalPages={state.data.totalPages} onChange={setPage} />
          <div className="table-responsive">
            <table className="table table-sm table-striped align-middle">
              <thead>
                <tr>
                  <th>Date &amp; time</th><th>Owner</th><th>City</th><th>Pet</th><th>Species / breed</th>
                  <th>Vet</th><th>Clinic</th><th>Reason</th>
                  {isPending && <th>Requested</th>}
                </tr>
              </thead>
              <tbody>
                {state.data.items.map((a) => (
                  <tr key={a.appointmentId}>
                    <td className="text-nowrap">{formatDateTime(a.scheduledAt)}</td>
                    <td>{a.ownerName}<br /><small className="text-muted">{a.ownerEmail}</small></td>
                    <td>{a.city}</td>
                    <td>{a.petName}</td>
                    <td>{a.species}<br /><small className="text-muted">{a.breed}</small></td>
                    <td>{a.vetName}</td>
                    <td>{a.clinic}</td>
                    <td>{a.reason}</td>
                    {isPending && <td>{formatDate(a.requestedAt)}</td>}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {state.data.items.length === 0 && <p className="text-muted">No appointments.</p>}
          <Pager page={state.data.page} totalPages={state.data.totalPages} onChange={setPage} />
        </>
      )}
    </>
  );
}
