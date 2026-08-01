import { useState } from 'react';
import './MenuItemForm.css';

// Prices live in state as STRINGS, not numbers, and are converted once on submit.
// A number input's value is a string anyway, and holding a number would fight the user mid-typing:
// "8." and "" are both un-numbers that a person passes through on the way to a valid price.
function initialFields(item, categories) {
  return {
    name: item?.name ?? '',
    description: item?.description ?? '',
    price: item ? String(item.price) : '',
    // A new item defaults to the first category rather than to blank, so the common case is
    // one less required choice. String() because a <select>'s value is always a string.
    categoryId: String(item?.categoryId ?? categories[0]?.id ?? ''),
    isAvailable: item?.isAvailable ?? true,
  };
}

export default function MenuItemForm({ item, categories, onSubmit, onCancel }) {
  const [fields, setFields] = useState(() => initialFields(item, categories));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const isEdit = Boolean(item);

  // One handler for every field: the input's name attribute selects which key to write.
  // Checkboxes carry their state on `checked`, not `value`, hence the type test.
  function handleChange(event) {
    const { name, value, type, checked } = event.target;
    setFields((current) => ({ ...current, [name]: type === 'checkbox' ? checked : value }));
  }

  function validate() {
    if (fields.name.trim().length === 0) return 'Give the item a name.';
    if (!fields.categoryId) return 'Pick a category.';

    // Number('') is 0, not NaN, so an empty box would silently pass a bare Number.isNaN check
    // and post a free item. Test the string for emptiness first.
    if (fields.price.trim().length === 0) return 'Give the item a price.';
    const price = Number(fields.price);
    if (Number.isNaN(price)) return 'Price must be a number.';
    if (price < 0) return 'Price cannot be negative.';
    return null;
  }

  async function handleSubmit(event) {
    // Without this the browser does a full page navigation on submit and React unmounts.
    event.preventDefault();

    const message = validate();
    if (message) {
      setError(message);
      return;
    }

    setSaving(true);
    setError(null);
    try {
      // The complete DTO every time - PUT is a full replace and POST needs all of it anyway,
      // so create and edit send the identical shape and only the verb differs.
      await onSubmit({
        categoryId: Number(fields.categoryId), // back to a number: the DTO's CategoryId is an int
        name: fields.name.trim(),
        description: fields.description.trim(),
        price: Number(fields.price),
        isAvailable: fields.isAvailable,
      });
    } catch (err) {
      // Shown in the form, not the page banner: the failure belongs to the thing being edited,
      // and the unsaved values are still on screen to correct.
      setError(err.message);
      setSaving(false); // stays mounted on failure, so this must not be in a finally that also runs on success
    }
  }

  return (
    <form className="item-form" onSubmit={handleSubmit}>
      <h2 className="item-form__title">{isEdit ? `Edit ${item.name}` : 'New item'}</h2>

      {error && (
        <p className="item-form__error" role="alert">
          {error}
        </p>
      )}

      {/* htmlFor, not for: `for` is a reserved word in JS, so JSX renames the attribute.
          Pairing it with the input's id is what makes the label text click into the field
          and what a screen reader reads when the field takes focus. */}
      <label className="item-form__field">
        <span className="item-form__label">Name</span>
        <input
          className="item-form__input"
          name="name"
          value={fields.name}
          onChange={handleChange}
          maxLength={100}
          autoFocus
        />
      </label>

      <label className="item-form__field">
        <span className="item-form__label">Description</span>
        <textarea
          className="item-form__input item-form__input--area"
          name="description"
          value={fields.description}
          onChange={handleChange}
          rows={3}
          maxLength={500}
        />
      </label>

      <div className="item-form__row">
        <label className="item-form__field">
          <span className="item-form__label">Price</span>
          <input
            className="item-form__input"
            name="price"
            // type="number" gives phones the numeric keypad and blocks letters.
            // step="0.01" is what stops the browser rejecting pence as invalid.
            type="number"
            step="0.01"
            min="0"
            value={fields.price}
            onChange={handleChange}
          />
        </label>

        <label className="item-form__field">
          <span className="item-form__label">Category</span>
          <select
            className="item-form__input"
            name="categoryId"
            value={fields.categoryId}
            onChange={handleChange}
          >
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      <label className="item-form__check">
        <input
          type="checkbox"
          name="isAvailable"
          checked={fields.isAvailable}
          onChange={handleChange}
        />
        <span>Available to order</span>
      </label>

      <div className="item-form__actions">
        <button type="submit" className="item-form__save" disabled={saving}>
          {saving ? 'Saving...' : 'Save'}
        </button>
        {/* type="button" matters: a button inside a form defaults to type="submit",
            so without it Cancel would submit the form it is meant to abandon. */}
        <button type="button" className="item-form__cancel" onClick={onCancel} disabled={saving}>
          Cancel
        </button>
      </div>
    </form>
  );
}
