import { ROLES } from '../api/auth.js';

// Where a role belongs when nothing more specific applies. Shared by LoginPage (post-login
// landing) and ProtectedRoute (wrong-role bounce) so the two can't disagree.
// A Customer has no dashboard, so the menu is the landing.
export function landingFor(role) {
  if (role === ROLES.Owner) return '/owner';
  if (role === ROLES.Driver) return '/driver';
  return '/';
}
