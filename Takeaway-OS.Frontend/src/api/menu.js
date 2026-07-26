import { api } from './client.js';

// GET /api/menuitems
// Returns: [{ id, categoryId, categoryName, name, description, price, isAvailable }]
export function getMenuItems() {
  return api.get('/api/menuitems', { auth: false });
}

// GET /api/menuitems/{id}
export function getMenuItem(id) {
  return api.get(`/api/menuitems/${id}`, { auth: false });
}

