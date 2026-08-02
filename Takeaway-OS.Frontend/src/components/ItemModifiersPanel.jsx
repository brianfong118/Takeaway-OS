import { useEffect, useState } from 'react';
import { getMenuItemAdmin } from '../api/menu.js';
import { getModifierGroups, linkModifierGroup, unlinkModifierGroup } from '../api/modifiers.js';
import { formatPriceDelta } from '../utils/format.js';
import './ItemModifiersPanel.css';

// Which option groups apply to one dish. Groups themselves are shared across items and are
// created in ModifierManager; this panel only connects and disconnects them.
export default function ItemModifiersPanel({ item, onClose }) {
  const [groups, setGroups] = useState([]);
  // A Set of linked group ids rather than a filtered list: the question this panel asks of every
  // row is "is this one linked", which is what a Set answers in one step instead of a scan.
  const [linkedIds, setLinkedIds] = useState(() => new Set());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busyIds, setBusyIds] = useState(() => new Set());

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        // getMenuItemAdmin, not getMenuItem: the admin read resolves for a disabled item and
        // returns inactive options too. The public one 404s here whenever the dish is hidden,
        // which is exactly when an owner is most likely to be editing it.
        const [detail, allGroups] = await Promise.all([getMenuItemAdmin(item.id), getModifierGroups()]);
        if (!active) return;
        setGroups(allGroups);
        setLinkedIds(new Set(detail.modifierGroups.map((g) => g.id)));
      } catch (err) {
        if (active) setError(err.message);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [item.id]);

  function setBusy(id, isBusy) {
    setBusyIds((current) => {
      const next = new Set(current);
      if (isBusy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  async function toggleLink(group) {
    const isLinked = linkedIds.has(group.id);
    setBusy(group.id, true);
    setError(null);
    try {
      if (isLinked) {
        await unlinkModifierGroup(item.id, group.id);
      } else {
        await linkModifierGroup(item.id, group.id);
      }
      // Applied after the server agrees, not before. Unlike a status flip on the order board
      // there is no queue of people waiting on this, and a failed link that had already drawn
      // itself as connected would be a lie about what the customer will see.
      setLinkedIds((current) => {
        const next = new Set(current);
        if (isLinked) next.delete(group.id);
        else next.add(group.id);
        return next;
      });
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(group.id, false);
    }
  }

  return (
    <section className="item-mods">
      <header className="item-mods__head">
        <div>
          <h2 className="item-mods__title">Options for {item.name}</h2>
          <p className="item-mods__hint">
            Tick the groups this dish should offer. Groups are shared, so editing one changes it
            everywhere it is used.
          </p>
        </div>
        <button type="button" className="item-mods__close" onClick={onClose}>
          Done
        </button>
      </header>

      {error && (
        <p className="item-mods__error" role="alert">
          {error}
        </p>
      )}

      {loading && <p className="item-mods__message">Loading options...</p>}

      {!loading && groups.length === 0 && (
        <p className="item-mods__message">
          No option groups exist yet. Create one under Options on the menu screen.
        </p>
      )}

      <ul className="item-mods__list">
        {groups.map((group) => {
          const linked = linkedIds.has(group.id);
          const busy = busyIds.has(group.id);
          return (
            <li key={group.id} className={`mod-link ${linked ? 'mod-link--on' : ''}`}>
              <label className="mod-link__label">
                <input
                  type="checkbox"
                  checked={linked}
                  disabled={busy}
                  onChange={() => toggleLink(group)}
                />
                <span className="mod-link__name">{group.name}</span>
                <span className="mod-link__rule">{describeRule(group)}</span>
              </label>

              <p className="mod-link__options">
                {group.options.length === 0 ? (
                  <span className="mod-link__empty">No options in this group yet</span>
                ) : (
                  group.options.map((option) => (
                    // Sibling-scoped key: option ids are unique within this list.
                    <span
                      key={option.id}
                      className={`mod-link__option ${option.isActive ? '' : 'mod-link__option--off'}`}
                    >
                      {option.name}
                      {/* formatPriceDelta returns "" for a free option, so nothing renders. */}
                      {option.priceDelta !== 0 && ` ${formatPriceDelta(option.priceDelta)}`}
                      {!option.isActive && ' (hidden)'}
                    </span>
                  ))
                )}
              </p>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

// Turns MinSelect/MaxSelect/IsRequired into the sentence the customer's picker will enforce,
// so the owner can see the effect without decoding three fields.
function describeRule(group) {
  const { minSelect, maxSelect, isRequired } = group;
  if (maxSelect === 1) return isRequired ? 'pick one, required' : 'pick up to one';
  if (isRequired || minSelect > 0) return `pick ${minSelect}-${maxSelect}, required`;
  return `pick up to ${maxSelect}`;
}
