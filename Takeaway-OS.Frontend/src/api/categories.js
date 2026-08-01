import { api } from './client.js';

// GET /api/categories
// Already sorted by DisplayOrder, which is the owner's intended menu order.
// Returns: [{ id, name, displayOrder }]
export function getCategories() {
  return api.get('/api/categories', { auth: false });
}

// --- Owner-only ---
// There is no admin list: GET /api/categories is unfiltered already (categories have no
// IsAvailable), so the owner screen reads the same endpoint the menu does.

// POST /api/categories
// Body: CategoryCreateDto { name, displayOrder }
// Returns: CategoryDto (201)
export function createCategory(category) {
  return api.post('/api/categories', category);
}

// PUT /api/categories/{id} -> 204
// Body: CategoryUpdateDto { name, displayOrder } - full replace, same as menu items.
export function updateCategory(id, category) {
  return api.put(`/api/categories/${id}`, category);
}

// DELETE /api/categories/{id} -> 204
// 409 while the category still holds menu items (FK is Restrict, so it is refused, not cascaded).
// Callers should surface that as "move or delete its items first", not as a generic failure.
export function deleteCategory(id) {
  return api.del(`/api/categories/${id}`);
}
