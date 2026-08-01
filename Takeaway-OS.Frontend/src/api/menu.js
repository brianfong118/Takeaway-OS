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

// --- Owner-only ---

// GET /api/menuitems/admin
// Returns: MenuItemDto[] including isAvailable: false ones, sorted by name (not by category).
export function getMenuItemsAdmin() {
  return api.get('/api/menuitems/admin');
}

// GET /api/menuitems/admin/{id}
// Returns: MenuItemAdminDetailDto - the item plus modifierGroups[], unfiltered: resolves for a
// disabled item, and each group's options[] include isActive: false ones.
export function getMenuItemAdmin(id) {
  return api.get(`/api/menuitems/admin/${id}`);
}

// POST /api/menuitems
// Body: MenuItemCreateDto { categoryId, name, description, price, isAvailable }
// Returns: MenuItemDto (201). 400 if categoryId does not exist.
export function createMenuItem(item) {
  return api.post('/api/menuitems', item);
}

// PUT /api/menuitems/{id} -> 204
// Body: MenuItemUpdateDto - full replace, there is no PATCH. The availability toggle must send the
// whole item; { isAvailable } alone would blank the name/description and zero the price.
export function updateMenuItem(id, item) {
  return api.put(`/api/menuitems/${id}`, item);
}

// DELETE /api/menuitems/{id} -> 204
// A hard delete. Order history survives it: OrderItems snapshot name and price and hold no FK here.
export function deleteMenuItem(id) {
  return api.del(`/api/menuitems/${id}`);
}

