import { api } from './client.js';

// --- Modifier groups ---

// GET /api/modifiergroups
// Returns: ModifierGroupDto[] { id, name, minSelect, maxSelect, isRequired, options[] }
// Options are nested and UNFILTERED here (isActive: false included), unlike the customer's
// item detail read, so this one list backs the whole owner modifier screen.
export function getModifierGroups() {
  return api.get('/api/modifiergroups');
}

// POST /api/modifiergroups
// Body: ModifierGroupCreateDto { name, minSelect, maxSelect, isRequired }
// Returns: ModifierGroupDto (201) with options: [] - a new group starts empty.
export function createModifierGroup(group) {
  return api.post('/api/modifiergroups', group);
}

// PUT /api/modifiergroups/{id} -> 204
// Body: ModifierGroupUpdateDto - full replace. Does not touch the group's options.
export function updateModifierGroup(id, group) {
  return api.put(`/api/modifiergroups/${id}`, group);
}

// DELETE /api/modifiergroups/{id} -> 204
// 409 while the group still has options OR is still linked to any menu item. Both FKs are
// Restrict, so a group has to be emptied and unlinked before it can go.
export function deleteModifierGroup(id) {
  return api.del(`/api/modifiergroups/${id}`);
}

// --- Modifier options ---
// No list read: options arrive nested inside getModifierGroups(), so fetching them
// separately would only create a second copy to keep in sync.

// POST /api/modifieroptions
// Body: ModifierOptionCreateDto { modifierGroupId, name, priceDelta, isActive }
// Returns: ModifierOptionDto (201). 400 if modifierGroupId does not exist.
export function createModifierOption(option) {
  return api.post('/api/modifieroptions', option);
}

// PUT /api/modifieroptions/{id} -> 204
// Body: ModifierOptionUpdateDto - includes modifierGroupId, so this can move an option
// between groups as well as edit it.
export function updateModifierOption(id, option) {
  return api.put(`/api/modifieroptions/${id}`, option);
}

// DELETE /api/modifieroptions/{id} -> 204
// A hard delete, and safe: OrderItemModifiers snapshot name and priceDelta with no FK back here,
// so past orders keep reading correctly. Deactivating (isActive: false) is still the reversible choice.
export function deleteModifierOption(id) {
  return api.del(`/api/modifieroptions/${id}`);
}

// --- Linking groups to a menu item ---

// POST /api/menuitems/{menuItemId}/modifiergroups/{modifierGroupId} -> 204
// 404 if either id is unknown, 409 if the link already exists.
export function linkModifierGroup(menuItemId, modifierGroupId) {
  return api.post(`/api/menuitems/${menuItemId}/modifiergroups/${modifierGroupId}`);
}

// DELETE /api/menuitems/{menuItemId}/modifiergroups/{modifierGroupId} -> 204
// Removes the link only. The group itself and its options survive for other items.
export function unlinkModifierGroup(menuItemId, modifierGroupId) {
  return api.del(`/api/menuitems/${menuItemId}/modifiergroups/${modifierGroupId}`);
}
