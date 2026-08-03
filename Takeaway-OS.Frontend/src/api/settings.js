import { api } from './client.js';

// GET /api/settings
// Returns: RestaurantSettingsDto { deliveryFee }
//
// The SAME value OrderService snapshots onto the order, so the fee quoted at checkout is the
// one Stripe charges. Anonymous, because the checkout has to quote it before an order exists.
export function getSettings() {
  return api.get('/api/settings', { auth: false });
}

// --- Owner-only ---

// PUT /api/settings
// Body: RestaurantSettingsUpdateDto { deliveryFee }
// Returns: RestaurantSettingsDto (the saved values), so the owner sees what the server holds.
// 400 if deliveryFee is outside 0-100. Only affects orders placed from now on; existing orders
// keep the fee they snapshotted.
export function updateSettings(settings) {
  return api.put('/api/settings', settings);
}
