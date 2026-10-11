import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useSession } from './useSession';

/**
 * Wraps every page that needs a signed-in agent. Without a session it sends the visitor to the
 * login page and remembers where they were going, so login can bring them back.
 */
export function RequireAuth() {
  const session = useSession();
  const location = useLocation();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
