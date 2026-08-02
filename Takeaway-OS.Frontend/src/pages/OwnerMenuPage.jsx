import { useCallback, useEffect, useState } from 'react';
import {
  getCategories,
  createCategory,
  updateCategory,
  deleteCategory,
} from '../api/categories.js';
import { getMenuItemsAdmin, createMenuItem, updateMenuItem, deleteMenuItem } from '../api/menu.js';
import {
  groupByCategory,
  orphanedItems,
  sortCategories,
  sortItems,
  toItemPayload,
} from '../utils/menu.js';
import { formatPrice } from '../utils/format.js';
import MenuItemForm from '../components/MenuItemForm.jsx';
import CategoryManager from '../components/CategoryManager.jsx';
import ModifierManager from '../components/ModifierManager.jsx';
import ItemModifiersPanel from '../components/ItemModifiersPanel.jsx';
import OwnerNav from '../components/OwnerNav.jsx';
import './OwnerMenuPage.css';

export default function OwnerMenuPage() {
  const [categories, setCategories] = useState([]);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busyIds, setBusyIds] = useState(() => new Set());
  const [confirmingDelete, setConfirmingDelete] = useState(null);
  // Which view is on screen. null = the list, otherwise a tagged object:
  //   { type: 'new' | 'categories' | 'modifiers' } | { type: 'edit' | 'links', item }
  // One piece of state rather than a set of booleans, which could contradict each other by
  // showing two panels at once. Tagged rather than a bare value because 'edit' and 'links' both
  // carry an item, so the item alone no longer says which panel is meant.
  const [panel, setPanel] = useState(null);

  // No polling here, unlike the order board. The menu changes when the owner changes it, and a
  // background refetch mid-edit would be a hazard rather than a help.
  const load = useCallback(async () => {
    const [categoryList, itemList] = await Promise.all([getCategories(), getMenuItemsAdmin()]);
    setCategories(categoryList);
    setItems(itemList);
    setError(null);
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        await load();
      } catch (err) {
        if (active) setError(err.message);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [load]);

  function setBusy(id, isBusy) {
    setBusyIds((current) => {
      const next = new Set(current);
      if (isBusy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  // Optimistic, same shape as the order board: flip the row, reconcile after, roll back only
  // this item if the PUT fails.
  async function toggleAvailability(item) {
    const nextAvailable = !item.isAvailable;
    setItems((current) =>
      current.map((i) => (i.id === item.id ? { ...i, isAvailable: nextAvailable } : i)),
    );
    setBusy(item.id, true);
    try {
      // The whole item, not just the changed field - PUT is a full replace (see api/menu.js).
      await updateMenuItem(item.id, toItemPayload(item, { isAvailable: nextAvailable }));
    } catch (err) {
      setItems((current) =>
        current.map((i) => (i.id === item.id ? { ...i, isAvailable: item.isAvailable } : i)),
      );
      setError(err.message);
    } finally {
      setBusy(item.id, false);
    }
  }

  // Not optimistic, deliberately: removing the row before the server agrees means a failed
  // delete has to resurrect it at the right position. Deletes are rare, so waiting is fine.
  async function handleDelete(item) {
    if (confirmingDelete !== item.id) {
      setConfirmingDelete(item.id);
      return;
    }
    setConfirmingDelete(null);
    setBusy(item.id, true);
    try {
      await deleteMenuItem(item.id);
      setItems((current) => current.filter((i) => i.id !== item.id));
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(item.id, false);
    }
  }

  // Errors are NOT caught here: the form awaits this and shows the failure next to the fields
  // the owner still needs to fix. Catching it would swallow that and leave the form thinking
  // it saved.
  async function handleSave(payload) {
    if (panel.type === 'new') {
      const created = await createMenuItem(payload);
      setItems((current) => sortItems([...current, created]));
    } else {
      const { item } = panel;
      await updateMenuItem(item.id, payload);
      // 204 carries no body, so the row is rebuilt locally. categoryName is re-derived rather
      // than kept from the old item, which would be stale if the category just changed.
      const categoryName =
        categories.find((c) => c.id === payload.categoryId)?.name ?? item.categoryName;
      // Sorted too: an edit can rename the item, which moves it within its section.
      setItems((current) =>
        sortItems(current.map((i) => (i.id === item.id ? { ...i, ...payload, categoryName } : i))),
      );
    }
    setPanel(null);
  }

  // The three below deliberately do not catch: CategoryManager shows the failure on the row it
  // belongs to, which is the only place the 409's "delete its items first" makes sense.
  async function handleCreateCategory(payload) {
    const created = await createCategory(payload);
    setCategories((current) => sortCategories([...current, created]));
  }

  async function handleUpdateCategory(id, payload) {
    await updateCategory(id, payload);
    setCategories((current) =>
      sortCategories(current.map((c) => (c.id === id ? { ...c, ...payload } : c))),
    );
  }

  async function handleDeleteCategory(id) {
    await deleteCategory(id);
    // Only reached on success. The API refuses (409) while the category still holds items, so
    // there is no orphaning to handle here - the FK guarantees the sections stay consistent.
    setCategories((current) => current.filter((c) => c.id !== id));
  }

  if (loading) return <p className="menu-admin__message">Loading menu...</p>;

  if (panel?.type === 'categories') {
    return (
      <div className="menu-admin">
        <OwnerNav />
        <CategoryManager
          categories={categories}
          onCreate={handleCreateCategory}
          onUpdate={handleUpdateCategory}
          onDelete={handleDeleteCategory}
          onClose={() => setPanel(null)}
        />
      </div>
    );
  }

  if (panel?.type === 'modifiers') {
    return (
      <div className="menu-admin">
        <OwnerNav />
        <ModifierManager onClose={() => setPanel(null)} />
      </div>
    );
  }

  if (panel?.type === 'links') {
    return (
      <div className="menu-admin">
        <OwnerNav />
        <ItemModifiersPanel
          key={panel.item.id}
          item={panel.item}
          onClose={() => setPanel(null)}
        />
      </div>
    );
  }

  if (panel) {
    return (
      <div className="menu-admin">
        <OwnerNav />
        <MenuItemForm
          // Remounts the form when switching straight from one item to another: without a key
          // React reuses the instance and useState's initial value is ignored on a re-render,
          // so the second item would open showing the first one's values.
          key={panel.type === 'new' ? 'new' : panel.item.id}
          item={panel.type === 'new' ? null : panel.item}
          categories={categories}
          onSubmit={handleSave}
          onCancel={() => setPanel(null)}
        />
      </div>
    );
  }

  const sections = groupByCategory(categories, items);
  const orphans = orphanedItems(categories, items);

  return (
    <div className="menu-admin">
      <OwnerNav />

      <header className="menu-admin__head">
        <h1 className="menu-admin__title">Menu</h1>
        <div className="menu-admin__actions">
          <button
            type="button"
            className="menu-admin__secondary"
            onClick={() => setPanel({ type: 'categories' })}
          >
            Categories
          </button>
          {/* "Option groups", not "Options": the row buttons are already called Options, and
              these two do different jobs - this one edits the shared groups themselves, the row
              one picks which of them a dish offers. */}
          <button
            type="button"
            className="menu-admin__secondary"
            onClick={() => setPanel({ type: 'modifiers' })}
          >
            Option groups
          </button>
          <button
            type="button"
            className="menu-admin__new"
            // An item must belong to a category, so with none there is nothing valid to create.
            disabled={categories.length === 0}
            onClick={() => setPanel({ type: 'new' })}
          >
            New item
          </button>
        </div>
      </header>

      {error && (
        <p className="menu-admin__error" role="alert">
          {error}
          <button type="button" className="menu-admin__retry" onClick={() => load().catch((e) => setError(e.message))}>
            Retry
          </button>
        </p>
      )}

      {sections.length === 0 && <p className="menu-admin__message">No categories yet.</p>}

      {sections.map(({ category, items: categoryItems }) => (
        <section key={category.id} className="menu-admin__section">
          <header className="menu-admin__section-head">
            <h2 className="menu-admin__category">{category.name}</h2>
            <span className="menu-admin__section-count">
              {categoryItems.length} {categoryItems.length === 1 ? 'item' : 'items'}
            </span>
          </header>

          {categoryItems.length === 0 ? (
            <p className="menu-admin__empty">Nothing in this category yet.</p>
          ) : (
            <ul className="menu-admin__list">
              {categoryItems.map((item) => (
                <MenuItemRow
                  key={item.id}
                  item={item}
                  busy={busyIds.has(item.id)}
                  confirmingDelete={confirmingDelete === item.id}
                  onToggle={() => toggleAvailability(item)}
                  onEdit={() => setPanel({ type: 'edit', item })}
                  onLinks={() => setPanel({ type: 'links', item })}
                  onDelete={() => handleDelete(item)}
                />
              ))}
            </ul>
          )}
        </section>
      ))}

      {orphans.length > 0 && (
        <section className="menu-admin__section">
          <h2 className="menu-admin__category">Uncategorised</h2>
          <ul className="menu-admin__list">
            {orphans.map((item) => (
              <MenuItemRow
                key={item.id}
                item={item}
                busy={busyIds.has(item.id)}
                confirmingDelete={confirmingDelete === item.id}
                onToggle={() => toggleAvailability(item)}
                onEdit={() => setPanel({ type: 'edit', item })}
                onLinks={() => setPanel({ type: 'links', item })}
                onDelete={() => handleDelete(item)}
              />
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

// Local to this page rather than in /components: nothing else renders a row like this, and it
// would only gain a props contract to maintain. Promote it if a second page ever needs it.
function MenuItemRow({ item, busy, confirmingDelete, onToggle, onEdit, onLinks, onDelete }) {
  return (
    <li className={`item-row ${item.isAvailable ? '' : 'item-row--off'}`}>
      <div className="item-row__main">
        <span className="item-row__name">{item.name}</span>
        {item.description && <span className="item-row__desc">{item.description}</span>}
      </div>

      <span className="item-row__price">{formatPrice(item.price)}</span>

      {/* role="switch" + aria-checked is what makes this announce as "on"/"off" rather than as a
          plain button. The visual track is drawn from the class alone. */}
      <button
        type="button"
        role="switch"
        aria-checked={item.isAvailable}
        className="item-row__switch"
        disabled={busy}
        onClick={onToggle}
      >
        <span className="item-row__switch-track" aria-hidden="true" />
        <span className="item-row__switch-label">{item.isAvailable ? 'Available' : 'Hidden'}</span>
      </button>

      <button type="button" className="item-row__edit" disabled={busy} onClick={onEdit}>
        Edit
      </button>

      {/* Named for what the customer sees ("Extra chicken +£1.50"), not for the schema's
          "modifier groups" - the owner never meets that word anywhere else. */}
      <button type="button" className="item-row__edit" disabled={busy} onClick={onLinks}>
        Options
      </button>

      <button
        type="button"
        className={`item-row__delete ${confirmingDelete ? 'item-row__delete--confirming' : ''}`}
        disabled={busy}
        onClick={onDelete}
      >
        {confirmingDelete ? 'Confirm' : 'Delete'}
      </button>
    </li>
  );
}
