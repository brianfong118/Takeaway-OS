import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth.js';

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

  if (roles && !roles.includes(user.role)) {
    return <Navigate to="/" replace />; // already logged in, so a login form would help nobody
  }

  return <Outlet />;
}
