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
