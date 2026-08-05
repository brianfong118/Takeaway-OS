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

// POST /api/auth/register
// Body: RegisterRequest { email, password, role, name, phone }
// Returns: AuthResponse { token, email, role } --> same shape login returns, and already
// signed in. The API creates the ApplicationUser and the linked Customer profile row together
// in one call, so there is no second "create profile" request to make afterwards.
//
// registerCustomer, fills in the role ITSELF rather than taking it as a parameter.
// That endpoint also accepts "Owner" and "Driver", and both are gated server-side (Owner only
// while zero Owners exist; Driver only for a caller who is already an Owner). Not exposing the
// role here means no screen in this app can even ask for one of them by accident
//
// auth: false for the same reason as login: creating an account cannot require already being
// signed in, and it keeps a stale token out of a request that has no use for one.
//
// Throws ApiError with status 400 when Identity rejects the details ie duplicate email or a
// password failing the policy. Both arrive as prose in the body, so err.message is showable.
export function registerCustomer({ email, password, name, phone }) {
  return api.post(
    '/api/auth/register',
    { email, password, name, phone, role: ROLES.Customer },
    { auth: false },
  );
}
