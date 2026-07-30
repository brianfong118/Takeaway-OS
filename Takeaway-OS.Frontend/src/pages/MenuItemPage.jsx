import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { getMenuItem } from '../api/menu.js';
import { useBasket } from '../hooks/useBasket.js';
import { MAX_LINE_QUANTITY, lineTotal } from '../utils/basket.js';
import { formatPrice, formatPriceDelta } from '../utils/format.js';
import './MenuItemPage.css';

// Mirrors OrderService: isRequired and minSelect are configured independently and can disagree,
// so the stricter of the two wins. Same rule enforced on both sides.
function effectiveMin(group) {
  return group.isRequired ? Math.max(group.minSelect, 1) : group.minSelect;
}

// "Choose 1", "Choose 1 to 3", "Choose up to 2", or "" when the group is a free-for-all.
function ruleHint(group) {
  const min = effectiveMin(group);
  if (min > 0 && min === group.maxSelect) return `Choose ${min}`;
  if (min > 0) return `Choose ${min} to ${group.maxSelect}`;
  return `Choose up to ${group.maxSelect}`;
}

export default function MenuItemPage() {
  const { id } = useParams(); // always a string, straight from the URL
  return <MenuItemView key={id} id={id} />;
}

function MenuItemView({ id }) {
  const { addLine } = useBasket();
  const navigate = useNavigate();

  const [item, setItem] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selections, setSelections] = useState({}); // { [groupId]: [optionId, ...] }
  const [quantity, setQuantity] = useState(1);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    let ignore = false;

    async function load() {
      try {
        const data = await getMenuItem(id);
        if (!ignore) setItem(data);
      } catch (err) {
        if (!ignore) setError(err); // the whole ApiError, so the 404 case can be told apart
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, [id]);

  function toggleOption(group, optionId) {
    // Updater form: React hands in the latest state rather than the value captured by this
    // render, which is what makes rapid clicks safe.
    setSelections((current) => {
      const selected = current[group.id] ?? [];

      if (selected.includes(optionId)) {
        return { ...current, [group.id]: selected.filter((x) => x !== optionId) };
      }
      if (group.maxSelect === 1) {
        return { ...current, [group.id]: [optionId] }; // single-select replaces instead of adding
      }
      if (selected.length >= group.maxSelect) {
        return current; // at the cap: ignore the click rather than silently dropping an earlier pick
      }
      return { ...current, [group.id]: [...selected, optionId] };
    });
  }

  // Declared here so it can close over the derived values computed below the early returns.
  function handleAdd(line) {
    addLine(line);
    navigate('/'); // back to the menu to keep ordering, rather than stranding them on this page
  }

  if (isLoading) return <p className="item__state">Loading...</p>;

  if (error) {
    // 404 covers both "no such id" and "disabled item", which the API deliberately conflates.
    const message =
      error.status === 404 ? 'That item is not on the menu.' : 'Could not load this item.';

    return (
      <div className="item__state item__state--error" role="alert">
        <p>{message}</p>
        <Link to="/" className="item__back">Back to menu</Link>
      </div>
    );
  }

  // Derived, not state. Safe below the early returns because these are plain values, not hooks.
  const selectedOptions = item.modifierGroups.flatMap((group) =>
    group.options.filter((option) => (selections[group.id] ?? []).includes(option.id)),
  );
  const unmetGroups = item.modifierGroups.filter(
    (group) => (selections[group.id] ?? []).length < effectiveMin(group),
  );

  // Snapshots name and prices, the same principle as OrderItems on the server: the basket keeps
  // what was shown at the time. The API still re-reads both from MenuItems when the order is
  // placed, so a price changed mid-basket is charged correctly.
  const lineToAdd = {
    menuItemId: item.id,
    name: item.name,
    unitPrice: item.price,
    quantity,
    notes: notes.trim(),
    modifiers: selectedOptions.map((o) => ({
      id: o.id,
      name: o.name,
      priceDelta: o.priceDelta,
    })),
  };

  const total = lineTotal(lineToAdd);

  return (
    <article className="item">
      <Link to="/" className="item__back">Back to menu</Link>

      <header className="item__header">
        <h1>{item.name}</h1>
        <p className="item__price">{formatPrice(item.price)}</p>
        {item.description && <p className="item__description">{item.description}</p>}
      </header>

      {item.modifierGroups.map((group) => {
        const selected = selections[group.id] ?? [];
        // Only a multi-select group can be "full". A pick-one group is never at cap for this
        // purpose: choosing another option replaces the current one, so disabling the others
        // would lock the customer into their first click (a radio cannot be unticked).
        const atCap = group.maxSelect > 1 && selected.length >= group.maxSelect;

        return (
          // fieldset/legend is the semantic grouping for a set of related choices,
          // and screen readers read the legend before each option in the group.
          <fieldset key={group.id} className="item__group">
            <legend className="item__group-legend">
              {group.name}
              <span className="item__group-rule">{ruleHint(group)}</span>
              {group.isRequired && <span className="item__group-required">Required</span>}
            </legend>

            {group.options.map((option) => {
              const isSelected = selected.includes(option.id);
              const delta = formatPriceDelta(option.priceDelta);

              return (
                <label key={option.id} className="item__option">
                  <input
                    // Radios for a pick-one group, checkboxes otherwise: the control itself
                    // tells the customer how many they may choose.
                    type={group.maxSelect === 1 ? 'radio' : 'checkbox'}
                    name={`group-${group.id}`}
                    checked={isSelected}
                    // Blocks new picks once the cap is reached, but never the ones already
                    // chosen, so the customer can always change their mind.
                    disabled={atCap && !isSelected}
                    onChange={() => toggleOption(group, option.id)}
                  />
                  <span className="item__option-name">{option.name}</span>
                  {delta && <span className="item__option-delta">{delta}</span>}
                </label>
              );
            })}
          </fieldset>
        );
      })}

      <div className="item__notes">
        <label htmlFor="notes">Anything else? (optional)</label>
        <textarea
          id="notes"
          className="item__notes-input"
          rows={2}
          maxLength={200}
          placeholder="e.g. no chilli"
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
        />
      </div>

      <div className="item__footer">
        <div className="item__quantity">
          <button
            type="button"
            onClick={() => setQuantity((q) => q - 1)}
            disabled={quantity <= 1}
            aria-label="Decrease quantity"
          >
            -
          </button>
          {/* aria-live so a screen reader announces the new number after a click. */}
          <span className="item__quantity-value" aria-live="polite">{quantity}</span>
          <button
            type="button"
            onClick={() => setQuantity((q) => q + 1)}
            disabled={quantity >= MAX_LINE_QUANTITY} // matches [Range(1, 5)] on the API
            aria-label="Increase quantity"
          >
            +
          </button>
        </div>

        <button
          type="button"
          className="item__add"
          onClick={() => handleAdd(lineToAdd)}
          disabled={unmetGroups.length > 0}
        >
          Add to basket - {formatPrice(total)}
        </button>
      </div>

      {unmetGroups.length > 0 && (
        <p className="item__unmet">
          Still to choose: {unmetGroups.map((g) => g.name).join(', ')}
        </p>
      )}
    </article>
  );
}
