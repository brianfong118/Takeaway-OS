import { api } from './client.js';

// Mirrors the Roles static class in the API. 
export const ROLES = {
  Owner: 'Owner',
  Driver: 'Driver',
  Customer: 'Customer',
};

// POST /api/auth/login
// Body: LoginRequest { email, password }
// Returns: AuthResponse { token, email, role }
// Throws ApiError with status 401 on wrong email or password - the API deliberately does not
// say which of the two was wrong, so the message shown to the user must not either.
//
// auth: false because logging in cannot require already being logged in. It also keeps a stale
// token out of the request, which would otherwise be sent and ignored.
export function login(email, password) {
  return api.post('/api/auth/login', { email, password }, { auth: false });
}
