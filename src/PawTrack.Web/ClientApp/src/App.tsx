import { useEffect } from 'react';
import { NavLink, Route, Routes, useLocation } from 'react-router-dom';
import VisitsPage from './pages/VisitsPage';
import OwnersPage from './pages/OwnersPage';
import AppointmentsPage from './pages/AppointmentsPage';

const titles: Record<string, string> = {
  '/': 'Past visits & invoices',
  '/appointments': 'Appointments',
  '/owners': 'Owners',
};

export default function App() {
  const { pathname } = useLocation();

  useEffect(() => {
    document.title = `${titles[pathname.toLowerCase()] ?? 'Not found'} - PawTrack`;
  }, [pathname]);

  return (
    <>
      <header>
        <nav className="navbar navbar-expand navbar-light bg-white border-bottom box-shadow mb-3">
          <div className="container">
            <NavLink className="navbar-brand" to="/">PawTrack</NavLink>
            <ul className="navbar-nav flex-grow-1">
              <li className="nav-item">
                <NavLink className="nav-link" to="/" end>Past visits</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link" to="/appointments">Appointments</NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link" to="/owners">Owners</NavLink>
              </li>
            </ul>
          </div>
        </nav>
      </header>
      <div className="container-fluid">
        <main role="main" className="pb-3">
          <Routes>
            <Route path="/" element={<VisitsPage />} />
            <Route path="/appointments" element={<AppointmentsPage />} />
            <Route path="/owners" element={<OwnersPage />} />
            <Route path="*" element={<h1 className="h3">Page not found</h1>} />
          </Routes>
        </main>
      </div>
      <footer className="border-top footer text-muted">
        <div className="container">&copy; 2026 - PawTrack</div>
      </footer>
    </>
  );
}
