import { api } from './client.js';

// The signed-in customer's address book. Like customers.js there is no customer id anywhere:
// the server scopes every one of these to the caller's own addresses, and an id belonging to
// someone else comes back as a 404 rather than a 403 therefore indistinguishable from one that does not
// exist, so nobody can probe another customer's book by guessing.

// GET /api/addresses
// Returns: AddressDto[] { id, label, line1, line2, city, postcode, isDefault }
// Ordered default-first, then by id, so the head of the list is the one to preselect.
export function getMyAddresses() {
  return api.get('/api/addresses');
}

// POST /api/addresses
// Body: AddressCreateDto { label, line1, line2, city, postcode, isDefault }
// Returns: the saved AddressDto (201).
export function createAddress(address) {
  return api.post('/api/addresses', address);
}

// PUT /api/addresses/{id}
// Body: AddressUpdateDto - the same fields as create. Returns the saved AddressDto.
export function updateAddress(id, address) {
  return api.put(`/api/addresses/${id}`, address);
}

// DELETE /api/addresses/{id} -> 204
export function deleteAddress(id) {
  return api.del(`/api/addresses/${id}`);
}
