// Categories arrive sorted by displayOrder, and GET /api/menuitems/admin sorts by name across the
// whole menu, so filtering per category keeps items A-Z inside each section without re-sorting.
//
// Empty categories are KEPT, unlike the customer menu which drops them: the owner needs to see a
// category they just made and has nothing in yet, and to see the items blocking a 409 on delete.
export function groupByCategory(categories, items) {
  return categories.map((category) => ({
    category,
    items: items.filter((item) => item.categoryId === category.id),
  }));
}

// An item whose categoryId matches no category can only appear if the category was deleted from
// under it, which the Restrict FK forbids. Collected anyway rather than silently dropped, so a
// stray row is visible and fixable instead of just missing from the page.
export function orphanedItems(categories, items) {
  const known = new Set(categories.map((category) => category.id));
  return items.filter((item) => !known.has(item.categoryId));
}

// GET /api/menuitems/admin sorts by name across the whole menu. A locally created or renamed item
// has to be re-sorted the same way, or it sits at the bottom of its section until the next reload
// and the list quietly reorders itself under the owner.
export function sortItems(items) {
  return [...items].sort((a, b) => a.name.localeCompare(b.name));
}

// GET /api/categories arrives sorted by displayOrder. After a local create or edit the page holds
// the list itself, so it has to re-apply that order; name is the tiebreak because displayOrder is
// free-form and duplicates are allowed.
export function sortCategories(categories) {
  return [...categories].sort(
    (a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name),
  );
}

// PUT /api/menuitems/{id} is a full replace, so every write has to send a complete item.
// Changing one field means spreading the loaded item and overriding, never posting the field alone.
export function toItemPayload(item, changes = {}) {
  return {
    categoryId: item.categoryId,
    name: item.name,
    description: item.description,
    price: item.price,
    isAvailable: item.isAvailable,
    ...changes,
  };
}
