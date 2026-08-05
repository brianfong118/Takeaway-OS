import { useCallback, useEffect, useState } from 'react';
import AccountNav from '../components/AccountNav.jsx';
import { createAddress, deleteAddress, getMyAddresses, updateAddress } from '../api/addresses.js';
import { getDeliveryAreas } from '../api/deliveryAreas.js';
import { looksOutsideArea } from '../utils/postcode.js';
import './AccountPage.css';
import './AddressesPage.css';

// Sentinel for "the form is open, but for a new address rather than an existing one". 
const NEW = 'new';

const EMPTY_FORM = { label: '', line1: '', line2: '', city: '', postcode: '', isDefault: false };

export default function AddressesPage() {
  const [addresses, setAddresses] = useState([]);
  const [areas, setAreas] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  // null = the form is closed. NEW = adding. A number = editing that address.
  // ONE piece of state rather than an `isAdding` bool plus an `editingId`, because those two
  // could contradict each other (both set) and this cannot.
  const [editing, setEditing] = useState(null);

  const load = useCallback(async () => {
    // allSettled: the address list is the page, the delivery areas are only an advisory marker,
    // so a failed areas fetch must not cost the addresses. Same call as CheckoutPage's load.
    const [addressResult, areaResult] = await Promise.allSettled([
      getMyAddresses(),
      getDeliveryAreas(),
    ]);

    if (addressResult.status === 'fulfilled') {
      setAddresses(addressResult.value);
      setError(null);
    } else {
      setError(addressResult.reason);
    }

    // Left as [] on failure, which silently drops the out-of-area markers. Advisory only, and
    // the server still refuses an out-of-area order, so losing it costs nothing that matters.
    if (areaResult.status === 'fulfilled') setAreas(areaResult.value);
  }, []);

  useEffect(() => {
    (async () => {
      await load();
      setIsLoading(false);
    })();
  }, [load]);

  async function handleSaved() {
    setEditing(null);
    await load();
  }

  if (isLoading) {
    return (
      <div className="account">
        <h1 className="account__title">Your account</h1>
        <AccountNav />
        <p className="account__status">Loading your addresses...</p>
      </div>
    );
  }

  return (
    <div className="account">
      <h1 className="account__title">Your account</h1>
      <AccountNav />

      {error && <p className="account__error" role="alert">{error.message}</p>}

      {addresses.length === 0 && editing === null && (
        <p className="addresses__empty">
          You haven’t saved any addresses yet. Saving one lets you pick it at checkout instead of
          typing it out.
        </p>
      )}

      <ul className="addresses__list">
        {addresses.map((address) =>
          editing === address.id ? (
            <li key={address.id}>
              <AddressForm
                address={address}
                onSaved={handleSaved}
                onCancel={() => setEditing(null)}
              />
            </li>
          ) : (
            <AddressRow
              key={address.id}
              address={address}
              // Computed here, in the list, rather than inside the row: the row would otherwise
              // need the whole areas array passed to it for a single boolean.
              outsideArea={looksOutsideArea(address.postcode, areas)}
              onEdit={() => setEditing(address.id)}
              onChanged={handleSaved}
            />
          ),
        )}
      </ul>

      {editing === NEW ? (
        <AddressForm onSaved={handleSaved} onCancel={() => setEditing(null)} />
      ) : (
        <button type="button" className="addresses__add" onClick={() => setEditing(NEW)}>
          Add an address
        </button>
      )}
    </div>
  );
}

// One address as it is displayed. Knows nothing about the API except the two writes it owns
// (delete, and promote-to-default); everything else is reported up via onChanged.
function AddressRow({ address, outsideArea, onEdit, onChanged }) {
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  async function handleDelete() {
    // Two-step, matching the owner's AreaRow. 
    // a district is trivial to re-add, whereas a saved address is the customer's own typing and
    // nothing on the server can reconstruct it.
    if (!confirming) return setConfirming(true);
    setConfirming(false);
    setBusy(true);
    setError(null);
    try {
      await deleteAddress(address.id);
      onChanged();
    } catch (err) {
      setError(err.message);
      setBusy(false); // not finally: onChanged() unmounts this row on the success path
    }
  }

  async function handleMakeDefault() {
    setBusy(true);
    setError(null);
    try {
      // A full update, because PUT replaces the whole address
      await updateAddress(address.id, { ...address, isDefault: true });
      onChanged();
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  }

  return (
    <li className="addresses__item">
      <div className="addresses__body">
        <p className="addresses__label">
          {/* The label is the customer's own nickname ("Home"), and it is optional - so it
              falls back to the first line rather than rendering an empty heading. */}
          {address.label || address.line1}
          {address.isDefault && <span className="addresses__badge">Default</span>}
        </p>

        {/* filter(Boolean) drops line2 when it is empty, so the address never reads ", ,". */}
        <p className="addresses__lines">
          {[address.line1, address.line2, address.city, address.postcode]
            .filter(Boolean)
            .join(', ')}
        </p>

        {outsideArea && (
          // Advisory, exactly like the checkout warning, and for the same reason: the delivery
          // area can change after an address is saved, so this is a heads-up rather than a
          // verdict. Nothing here stops them keeping the address or using it for collection.
          <p className="addresses__warning">
            We don’t currently deliver to this postcode. You can still order for collection.
          </p>
        )}

        {error && <p className="addresses__error" role="alert">{error}</p>}
      </div>

      <div className="addresses__actions">
        {!address.isDefault && (
          <button type="button" className="addresses__action" disabled={busy} onClick={handleMakeDefault}>
            Make default
          </button>
        )}

        <button type="button" className="addresses__action" disabled={busy} onClick={onEdit}>
          Edit
        </button>

        <button
          type="button"
          className={`addresses__action addresses__action--danger ${confirming ? 'addresses__action--confirming' : ''}`}
          disabled={busy}
          onClick={handleDelete}
          aria-label={`Delete the address at ${address.line1}`}
        >
          {confirming ? 'Confirm' : 'Delete'}
        </button>
      </div>
    </li>
  );
}

// Add and edit are the same form: the fields are identical, and the only difference is whether
// there is an id to PUT to. Two components would be two places to add a field to.
function AddressForm({ address, onSaved, onCancel }) {
  // Lazy initialiser, so the seeding runs on the first render only , passing the object directly
  // would rebuild it every render and throw away what had been typed.
  const [form, setForm] = useState(() =>
    address
      ? {
          label: address.label,
          line1: address.line1,
          line2: address.line2,
          city: address.city,
          postcode: address.postcode,
          isDefault: address.isDefault,
        }
      : EMPTY_FORM,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const isEdit = Boolean(address);

  function handleChange(event) {
    // A checkbox reports its state in `checked`, not `value` (`value` is the string that would
    // be submitted by a native form post, which is "on" unless one is set). So the field's type
    // decides which property to read - the one place the generic handler cannot stay type-blind.
    const { name, value, type, checked } = event.target;
    setForm((current) => ({ ...current, [name]: type === 'checkbox' ? checked : value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (busy) return;

    setBusy(true);
    setError(null);

    const payload = {
      label: form.label.trim(),
      line1: form.line1.trim(),
      line2: form.line2.trim(),
      city: form.city.trim(),
      postcode: form.postcode.trim(),
      isDefault: form.isDefault,
    };

    try {
      if (isEdit) await updateAddress(address.id, payload);
      else await createAddress(payload);
      onSaved();
    } catch (err) {
      // 400 here is the postcode check, and the message names the problem in prose.
      setError(err.message);
      setBusy(false); // not finally: onSaved() unmounts this form on the success path
    }
  }

  return (
    <form className="address-form" onSubmit={handleSubmit}>
      <h2 className="address-form__title">{isEdit ? 'Edit address' : 'New address'}</h2>

      {error && <p className="addresses__error" role="alert">{error}</p>}

      <div className="account__field">
        <label htmlFor="label">Label (optional)</label>
        <input
          id="label"
          name="label"
          type="text"
          maxLength={50}
          placeholder="Home, Work"
          value={form.label}
          onChange={handleChange}
        />
      </div>

      <div className="account__field">
        <label htmlFor="line1">Address line 1</label>
        <input
          id="line1"
          name="line1"
          type="text"
          required
          maxLength={100}
          autoComplete="address-line1"
          value={form.line1}
          onChange={handleChange}
        />
      </div>

      <div className="account__field">
        <label htmlFor="line2">Address line 2 (optional)</label>
        <input
          id="line2"
          name="line2"
          type="text"
          maxLength={100}
          autoComplete="address-line2"
          value={form.line2}
          onChange={handleChange}
        />
      </div>

      <div className="account__field">
        <label htmlFor="city">Town or city</label>
        <input
          id="city"
          name="city"
          type="text"
          required
          maxLength={100}
          autoComplete="address-level2"
          value={form.city}
          onChange={handleChange}
        />
      </div>

      <div className="account__field">
        <label htmlFor="postcode">Postcode</label>
        <input
          id="postcode"
          name="postcode"
          type="text"
          required
          maxLength={10} // mirrors [MaxLength(10)] on AddressCreateDto
          autoComplete="postal-code"
          placeholder="E1 6AN"
          className="address-form__postcode"
          value={form.postcode}
          onChange={handleChange}
        />
      </div>

      {/* checked, not value - see handleChange. Wrapped in the label so the text is part of the
          click target, which is what makes a checkbox usable on a phone. */}
      <label className="address-form__check">
        <input
          type="checkbox"
          name="isDefault"
          checked={form.isDefault}
          onChange={handleChange}
        />
        <span>Use this as my default address</span>
      </label>

      <div className="address-form__actions">
        <button type="submit" className="account__submit" disabled={busy}>
          {busy ? 'Saving...' : 'Save address'}
        </button>
        <button type="button" className="addresses__action" disabled={busy} onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  );
}
