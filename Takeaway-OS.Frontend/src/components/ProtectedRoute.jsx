import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth.js';
import { landingFor } from '../utils/routes.js';

// Route guard for the UI only. It hides pages, it does not protect data: every endpoint those
// pages call is [Authorize]d server-side, which is the check that actually counts.
//
// roles: optional array of role names allowed through. Omitted = any logged-in user.
export default function ProtectedRoute({ roles }) {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    // state rides on the history entry, not the URL, so LoginPage can send them back afterwards.
    // replace, so Back from the login page doesn't bounce off this guard again.
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  // Already logged in, so a login form would help nobody. Their own landing rather than "/",
  // because this fires on a stale `from` too: log out on /owner, log back in as a Driver, and
  // LoginPage would otherwise send them to /owner and this guard would strand them on the menu.
  if (roles && !roles.includes(user.role)) {
    return <Navigate to={landingFor(user.role)} replace />;
  }

  return <Outlet />;
}
