import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { clearSession } from './auth/sessionStore';
import { useSession } from './auth/useSession';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const session = useSession();
  const navigate = useNavigate();

  const signOut = () => {
    // Go to the login page first, so signing out does not remember this page as "where to return".
    void navigate('/login', { replace: true });
    clearSession();
  };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {session && (
          <div className="app__user">
            <span>
              Signed in as <strong>{session.user.fullName}</strong>
            </span>
            <button type="button" className="button button--ghost" onClick={signOut}>
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
