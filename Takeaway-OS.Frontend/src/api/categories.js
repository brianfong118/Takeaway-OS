import { api } from './client.js';

// GET /api/categories
// Already sorted by DisplayOrder, which is the owner's intended menu order.
// Returns: [{ id, name, displayOrder }]
export function getCategories() {
  return api.get('/api/categories', { auth: false });
}
