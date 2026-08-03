import { api } from './client.js';

// GET /api/deliveryareas
// Returns: DeliveryAreaDto[] { id, outwardCode }, alphabetical
export function getDeliveryAreas() {
  return api.get('/api/deliveryareas', { auth: false });
}

// --- Owner-only ---
// POST /api/deliveryareas
// Body: DeliveryAreaCreateDto { outwardCode } - the part BEFORE the space ("E1", not "E1 6AN")
// Returns: DeliveryAreaDto (201). 400 if it isn't a valid district, 409 if it's already listed.
// The server normalises, so "  e1  " is stored and returned as "E1".
export function createDeliveryArea(area) {
  return api.post('/api/deliveryareas', area);
}

// DELETE /api/deliveryareas/{id} -> 204
// Removing the last district stops delivery entirely rather than reverting to "everywhere".
export function deleteDeliveryArea(id) {
  return api.del(`/api/deliveryareas/${id}`);
}
