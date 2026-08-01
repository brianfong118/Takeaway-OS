import { api } from './client.js';

// GET /api/openinghours/status
// Returns: RestaurantStatusDto { isOpen, message }
//
// This is the SAME IBusinessHoursService.GetStatusAsync() that OrderService calls before
// accepting an order, so the banner this drives can't claim "open" while the API rejects
// the order as closed. Keep it pointed at this endpoint rather than deriving open/closed
// from the raw schedule in the browser — that would be a second, drifting implementation.
export function getRestaurantStatus() {
  return api.get('/api/openinghours/status', { auth: false });
}

// GET /api/openinghours
// Returns: OpeningHoursDto[] { id, dayOfWeek, openTime, closeTime }
// dayOfWeek is the enum NAME ("Monday"), and the times are TimeOnly, so they arrive as
// "17:00:00" - one field wider than the "17:00" an <input type="time"> reads and writes.
export function getSchedule() {
  return api.get('/api/openinghours', { auth: false });
}

// --- Owner-only ---

// POST /api/openinghours
// Body: OpeningHoursCreateDto { dayOfWeek, openTime, closeTime }
// Returns: OpeningHoursDto (201). 400 with a reason if the window overlaps an existing one or
// is zero-length (openTime == closeTime). closeTime <= openTime is how a past-midnight window
// is expressed, so it is valid, not an error.
export function createWindow(window) {
  return api.post('/api/openinghours', window);
}

// PUT /api/openinghours/{id} -> 204. Same 400 rules as create; 404 if the window is gone.
export function updateWindow(id, window) {
  return api.put(`/api/openinghours/${id}`, window);
}

// DELETE /api/openinghours/{id} -> 204
// Removing every window for a day is legal and simply means closed that day.
export function deleteWindow(id) {
  return api.del(`/api/openinghours/${id}`);
}

// PUT /api/openinghours/closure
// Body: ClosureUpdateDto { isTemporarilyClosed, closureReason }
// Returns: RestaurantStatusDto - the resulting status, so the owner sees what customers see.
// Idempotent both ways; reopening clears the reason, so a stale one cannot resurface.
export function setClosure(closure) {
  return api.put('/api/openinghours/closure', closure);
}
