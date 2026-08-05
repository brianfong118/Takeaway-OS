import { api } from './client.js';

// The signed-in customer's own profile. Every route here is "me" 
// no /api/customers/{id} on the server, so there is no id for this file to pass and none for a
// caller to tamper with. The owning customer is read from the JWT on every request.

// GET /api/customers/me
// Returns: CustomerProfileDto { id, name, phone, email }
//
// Note email is present here but NOT editable below. It is the Identity username the customer
// signs in with, so changing it is an auth concern (uniqueness, re-verification) rather than a
// profile edit (which is why CustomerProfileUpdateDto has no email field at all.)
//
// 404 when a Customer-role login has no Customer row. That should be impossible (register
// creates both together) but it is a real response, so the page has to survive it.
export function getMyProfile() {
  return api.get('/api/customers/me');
}

// PUT /api/customers/me
// Body: CustomerProfileUpdateDto { name, phone }, both [Required] server-side
// Returns: the saved CustomerProfileDto, so the page needs no follow-up GET.
export function updateMyProfile(profile) {
  return api.put('/api/customers/me', profile);
}
