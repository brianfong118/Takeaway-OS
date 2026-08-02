import { useEffect, useState } from 'react';
import {
  getModifierGroups,
  createModifierGroup,
  updateModifierGroup,
  deleteModifierGroup,
  createModifierOption,
  updateModifierOption,
  deleteModifierOption,
} from '../api/modifiers.js';
import { formatPriceDelta } from '../utils/format.js';
import './ModifierManager.css';

// Option groups and their options, restaurant-wide. Which dishes offer which group is set per
// item in ItemModifiersPanel, not here - a group is reusable by design.
export default function ModifierManager({ onClose }) {
  const [groups, setGroups] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const data = await getModifierGroups();
        if (active) setGroups(data);
      } catch (err) {
        if (active) setError(err.message);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  // Replaces one group in place. Every child mutation funnels through here so the nested
  // options array is only ever rebuilt in one function.
  function replaceGroup(id, updater) {
    setGroups((current) => current.map((g) => (g.id === id ? updater(g) : g)));
  }

  async function handleAddGroup(payload) {
    const created = await createModifierGroup(payload);
    setGroups((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
  }

  return (
    <section className="mod-manager">
      <header className="mod-manager__head">
        <div>
          <h2 className="mod-manager__title">Option groups</h2>
          <p className="mod-manager__hint">
            Groups are shared across dishes. Editing one changes every dish that offers it.
          </p>
        </div>
        <button type="button" className="mod-manager__close" onClick={onClose}>
          Done
        </button>
      </header>

      {error && (
        <p className="mod-manager__error" role="alert">
          {error}
        </p>
      )}

      {loading ? (
        <p className="mod-manager__message">Loading options...</p>
      ) : (
        <>
          {groups.length === 0 && <p className="mod-manager__message">No option groups yet.</p>}

          <ul className="mod-manager__list">
            {groups.map((group) => (
              <GroupCard
                key={group.id}
                group={group}
                onGroupChanged={(patch) => replaceGroup(group.id, (g) => ({ ...g, ...patch }))}
                onGroupDeleted={() =>
                  setGroups((current) => current.filter((g) => g.id !== group.id))
                }
                onOptionsChanged={(options) => replaceGroup(group.id, (g) => ({ ...g, options }))}
              />
            ))}
          </ul>

          <NewGroupRow onAdd={handleAddGroup} />
        </>
      )}
    </section>
  );
}

function GroupCard({ group, onGroupChanged, onGroupDeleted, onOptionsChanged }) {
  const [name, setName] = useState(group.name);
  const [minSelect, setMinSelect] = useState(String(group.minSelect));
  const [maxSelect, setMaxSelect] = useState(String(group.maxSelect));
  const [isRequired, setIsRequired] = useState(group.isRequired);
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  const dirty =
    name !== group.name ||
    Number(minSelect) !== group.minSelect ||
    Number(maxSelect) !== group.maxSelect ||
    isRequired !== group.isRequired;

  async function handleSave() {
    if (name.trim().length === 0) return setError('Give the group a name.');
    const min = Number(minSelect) || 0;
    const max = Number(maxSelect) || 0;
    // Checked here because the API does not: a group with max below min can never be satisfied,
    // and the customer would meet a picker that refuses every choice.
    if (max < min) return setError('Max cannot be less than min.');

    const payload = { name: name.trim(), minSelect: min, maxSelect: max, isRequired };
    setBusy(true);
    setError(null);
    try {
      await updateModifierGroup(group.id, payload);
      onGroupChanged(payload);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete() {
    if (!confirming) return setConfirming(true);
    setConfirming(false);
    setBusy(true);
    setError(null);
    try {
      await deleteModifierGroup(group.id);
      onGroupDeleted();
    } catch (err) {
      // 409 while the group still holds options or is linked to a dish. The API's message names
      // the blocker, so it is shown unchanged.
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="mod-group">
      <div className="mod-group__fields">
        <input
          className="mod-group__name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          aria-label={`Name for ${group.name}`}
          maxLength={60}
          disabled={busy}
        />
        <label className="mod-group__num">
          <span>Min</span>
          <input
            type="number"
            min="0"
            value={minSelect}
            onChange={(e) => setMinSelect(e.target.value)}
            aria-label={`Minimum selections for ${group.name}`}
            disabled={busy}
          />
        </label>
        <label className="mod-group__num">
          <span>Max</span>
          <input
            type="number"
            min="0"
            value={maxSelect}
            onChange={(e) => setMaxSelect(e.target.value)}
            aria-label={`Maximum selections for ${group.name}`}
            disabled={busy}
          />
        </label>
        <label className="mod-group__req">
          <input
            type="checkbox"
            checked={isRequired}
            onChange={(e) => setIsRequired(e.target.checked)}
            disabled={busy}
          />
          <span>Required</span>
        </label>

        <button
          type="button"
          className="mod-group__save"
          disabled={!dirty || busy}
          onClick={handleSave}
        >
          Save
        </button>
        <button
          type="button"
          className={`mod-group__delete ${confirming ? 'mod-group__delete--confirming' : ''}`}
          disabled={busy}
          onClick={handleDelete}
        >
          {confirming ? 'Confirm' : 'Delete'}
        </button>
      </div>

      {error && (
        <p className="mod-group__error" role="alert">
          {error}
        </p>
      )}

      <ul className="mod-group__options">
        {group.options.map((option) => (
          <OptionRow
            key={option.id}
            option={option}
            onChanged={(patch) =>
              onOptionsChanged(
                group.options.map((o) => (o.id === option.id ? { ...o, ...patch } : o)),
              )
            }
            onDeleted={() => onOptionsChanged(group.options.filter((o) => o.id !== option.id))}
          />
        ))}
      </ul>

      <NewOptionRow
        groupId={group.id}
        onAdded={(created) => onOptionsChanged([...group.options, created])}
      />
    </li>
  );
}

function OptionRow({ option, onChanged, onDeleted }) {
  const [name, setName] = useState(option.name);
  const [priceDelta, setPriceDelta] = useState(String(option.priceDelta));
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  const dirty = name !== option.name || Number(priceDelta) !== option.priceDelta;

  // The payload always carries modifierGroupId, because ModifierOptionUpdateDto includes it and
  // PUT is a full replace here too - omitting it would move the option to group 0.
  function payload(overrides = {}) {
    return {
      modifierGroupId: option.modifierGroupId,
      name: name.trim(),
      priceDelta: Number(priceDelta) || 0,
      isActive: option.isActive,
      ...overrides,
    };
  }

  async function save(overrides) {
    const body = payload(overrides);
    if (body.name.length === 0) return setError('Give the option a name.');
    setBusy(true);
    setError(null);
    try {
      await updateModifierOption(option.id, body);
      onChanged(body);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete() {
    if (!confirming) return setConfirming(true);
    setConfirming(false);
    setBusy(true);
    setError(null);
    try {
      await deleteModifierOption(option.id);
      onDeleted();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className={`mod-option ${option.isActive ? '' : 'mod-option--off'}`}>
      <div className="mod-option__fields">
        <input
          className="mod-option__name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          aria-label={`Name for option ${option.name}`}
          maxLength={60}
          disabled={busy}
        />
        <input
          className="mod-option__price"
          type="number"
          step="0.01"
          value={priceDelta}
          onChange={(e) => setPriceDelta(e.target.value)}
          aria-label={`Price change for ${option.name}`}
          disabled={busy}
        />
        <span className="mod-option__preview">{formatPriceDelta(option.priceDelta) || 'free'}</span>

        <button
          type="button"
          className="mod-option__save"
          disabled={!dirty || busy}
          onClick={() => save()}
        >
          Save
        </button>
        {/* Deactivating is the reversible choice and is offered first; delete is beside it for
            an option that was simply a mistake. */}
        <button
          type="button"
          className="mod-option__toggle"
          disabled={busy}
          onClick={() => save({ isActive: !option.isActive })}
        >
          {option.isActive ? 'Hide' : 'Show'}
        </button>
        <button
          type="button"
          className={`mod-option__delete ${confirming ? 'mod-option__delete--confirming' : ''}`}
          disabled={busy}
          onClick={handleDelete}
        >
          {confirming ? 'Confirm' : 'Delete'}
        </button>
      </div>

      {error && (
        <p className="mod-group__error" role="alert">
          {error}
        </p>
      )}
    </li>
  );
}

function NewOptionRow({ groupId, onAdded }) {
  const [name, setName] = useState('');
  const [priceDelta, setPriceDelta] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function handleAdd(event) {
    event.preventDefault();
    if (name.trim().length === 0) return setError('Give the option a name.');
    setBusy(true);
    setError(null);
    try {
      const created = await createModifierOption({
        modifierGroupId: groupId,
        name: name.trim(),
        priceDelta: Number(priceDelta) || 0, // blank means free, which is a normal "remove X" option
        isActive: true,
      });
      onAdded(created);
      setName('');
      setPriceDelta('');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="mod-new-option" onSubmit={handleAdd}>
      <input
        className="mod-option__name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Add an option"
        aria-label="New option name"
        maxLength={60}
        disabled={busy}
      />
      <input
        className="mod-option__price"
        type="number"
        step="0.01"
        value={priceDelta}
        onChange={(e) => setPriceDelta(e.target.value)}
        placeholder="0.00"
        aria-label="New option price change"
        disabled={busy}
      />
      <button type="submit" className="mod-option__add" disabled={busy}>
        {busy ? 'Adding...' : 'Add'}
      </button>
      {error && (
        <p className="mod-group__error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}

function NewGroupRow({ onAdd }) {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function handleAdd(event) {
    event.preventDefault();
    if (name.trim().length === 0) return setError('Give the group a name.');
    setBusy(true);
    setError(null);
    try {
      // Sensible starting rule: optional, pick at most one. The owner adjusts it on the row that
      // appears, which is less to fill in up front than four fields for every new group.
      await onAdd({ name: name.trim(), minSelect: 0, maxSelect: 1, isRequired: false });
      setName('');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="mod-new-group" onSubmit={handleAdd}>
      <input
        className="mod-group__name"
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="New option group"
        aria-label="New group name"
        maxLength={60}
        disabled={busy}
      />
      <button type="submit" className="mod-group__add" disabled={busy}>
        {busy ? 'Adding...' : 'Add group'}
      </button>
      {error && (
        <p className="mod-group__error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}
