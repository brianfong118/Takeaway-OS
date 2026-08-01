import { useState } from 'react';
import './CategoryManager.css';

export default function CategoryManager({ categories, onCreate, onUpdate, onDelete, onClose }) {
  return (
    <section className="cat-manager">
      <header className="cat-manager__head">
        <h2 className="cat-manager__title">Categories</h2>
        <button type="button" className="cat-manager__close" onClick={onClose}>
          Done
        </button>
      </header>

      <p className="cat-manager__hint">
        Order sets where a category appears on the customer menu, lowest first.
      </p>

      <ul className="cat-manager__list">
        {categories.map((category) => (
          // key on the id, so editing one row's name never makes React reuse another row's
          // draft state - index keys would do exactly that after a delete.
          <CategoryRow
            key={category.id}
            category={category}
            onSave={(payload) => onUpdate(category.id, payload)}
            onDelete={() => onDelete(category.id)}
          />
        ))}
      </ul>

      <NewCategoryRow onCreate={onCreate} />
    </section>
  );
}

function CategoryRow({ category, onSave, onDelete }) {
  const [name, setName] = useState(category.name);
  const [displayOrder, setDisplayOrder] = useState(String(category.displayOrder));
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  // Save is offered only when something actually differs, so a row of unchanged categories
  // has no live buttons to press by accident.
  const dirty =
    name !== category.name || Number(displayOrder) !== category.displayOrder;

  async function handleSave() {
    if (name.trim().length === 0) {
      setError('Give the category a name.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await onSave({ name: name.trim(), displayOrder: Number(displayOrder) || 0 });
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete() {
    if (!confirming) {
      setConfirming(true);
      return;
    }
    setConfirming(false);
    setBusy(true);
    setError(null);
    try {
      await onDelete();
    } catch (err) {
      // The 409 case: the API's own message names the blocker, so it is shown as-is rather
      // than replaced with a generic "could not delete".
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="cat-row">
      <div className="cat-row__fields">
        <input
          className="cat-row__name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          aria-label={`Name for ${category.name}`}
          maxLength={60}
          disabled={busy}
        />
        <input
          className="cat-row__order"
          type="number"
          value={displayOrder}
          onChange={(e) => setDisplayOrder(e.target.value)}
          aria-label={`Display order for ${category.name}`}
          disabled={busy}
        />

        <button
          type="button"
          className="cat-row__save"
          disabled={!dirty || busy}
          onClick={handleSave}
        >
          Save
        </button>

        <button
          type="button"
          className={`cat-row__delete ${confirming ? 'cat-row__delete--confirming' : ''}`}
          disabled={busy}
          onClick={handleDelete}
        >
          {confirming ? 'Confirm' : 'Delete'}
        </button>
      </div>

      {error && (
        <p className="cat-row__error" role="alert">
          {error}
        </p>
      )}
    </li>
  );
}

function NewCategoryRow({ onCreate }) {
  const [name, setName] = useState('');
  const [displayOrder, setDisplayOrder] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function handleAdd(event) {
    event.preventDefault();
    if (name.trim().length === 0) {
      setError('Give the category a name.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await onCreate({ name: name.trim(), displayOrder: Number(displayOrder) || 0 });
      setName(''); // cleared only on success, so a failed add keeps what was typed
      setDisplayOrder('');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  // A form, unlike the rows above: this is the one place where pressing Enter has an obvious
  // meaning, and a form gives that for free.
  return (
    <form className="cat-new" onSubmit={handleAdd}>
      <div className="cat-row__fields">
        <input
          className="cat-row__name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="New category"
          aria-label="New category name"
          maxLength={60}
          disabled={busy}
        />
        <input
          className="cat-row__order"
          type="number"
          value={displayOrder}
          onChange={(e) => setDisplayOrder(e.target.value)}
          placeholder="0"
          aria-label="New category display order"
          disabled={busy}
        />
        <button type="submit" className="cat-row__add" disabled={busy}>
          {busy ? 'Adding...' : 'Add'}
        </button>
      </div>

      {error && (
        <p className="cat-row__error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}
