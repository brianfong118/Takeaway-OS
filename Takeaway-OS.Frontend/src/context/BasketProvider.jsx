import { useEffect, useMemo, useReducer } from 'react';
import { BasketContext } from './BasketContext.js';
import { MAX_LINE_QUANTITY, basketFingerprint, lineKey, lineTotal } from '../utils/basket.js';

const STORAGE_KEY = 'takeawayos.basket';

function clampQuantity(quantity) {
  return Math.min(Math.max(quantity, 1), MAX_LINE_QUANTITY);
}

// Every basket change goes through here
// State is the lines array. Never mutated: each case returns a new array or the old one.
function basketReducer(lines, action) {
  switch (action.type) {
    case 'add': {
      // Id is derived here rather than by the caller, so the merge rule cannot be
      // applied inconsistently from different pages.
      const line = { ...action.line, key: lineKey(action.line) };
      const existing = lines.find((l) => l.key === line.key);

      if (!existing) return [...lines, line];

      // Same item, same options, same note: bump the existing row instead of adding a
      // duplicate the customer would have to delete twice.
      return lines.map((l) =>
        l.key === line.key ? { ...l, quantity: clampQuantity(l.quantity + line.quantity) } : l,
      );
    }

    case 'setQuantity':
      return lines.map((l) =>
        l.key === action.key ? { ...l, quantity: clampQuantity(action.quantity) } : l,
      );

    case 'remove':
      return lines.filter((l) => l.key !== action.key);

    case 'clear':
      return [];

    // Empty the basket only if it still holds exactly what was ordered (see basketFingerprint).
    // Decided HERE, inside the reducer, because the reducer is the one place that always sees the
    // CURRENT lines. A component deciding it would need `lines` in an effect's dependency array,
    // re-running the effect on every basket change. Returning the same `lines` array when it
    // doesn't match means React sees no change and re-renders nothing.
    case 'clearIfUnchanged':
      return basketFingerprint(lines) === action.fingerprint ? [] : lines;

    default:
      // A typo in an action type would otherwise fail silently by returning the old state.
      throw new Error(`Unknown basket action: ${action.type}`);
  }
}

// Third argument to useReducer: a lazy initialiser, run once on mount instead of on every
// render like a plain initial value would be.
function loadLines() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return [];
    const parsed = JSON.parse(stored);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return []; // corrupt or hand-edited storage starts an empty basket rather than crashing the app
  }
}

export function BasketProvider({ children }) {
  const [lines, dispatch] = useReducer(basketReducer, undefined, loadLines);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
  }, [lines]); // runs after any change, so the basket survives a refresh

  // Components dispatch through these instead of touching dispatch directly, so action
  // shapes stay an internal detail of this file.
  //
  // Built ONCE (empty dependency array) rather than inside the `value` memo below, so each function
  // keeps the same identity for the provider's whole life. That matters to any consumer that calls
  // one from a useEffect: the function then belongs in the effect's dependency array, and if its
  // identity changed every time the basket did, calling clearBasket() inside an effect would change
  // the basket, hand the effect a "new" clearBasket, and re-run the effect. PaymentPage does exactly
  // that. Safe to build once because dispatch is guaranteed stable by React.
  const actions = useMemo(
    () => ({
      addLine: (line) => dispatch({ type: 'add', line }),
      setLineQuantity: (key, quantity) => dispatch({ type: 'setQuantity', key, quantity }),
      removeLine: (key) => dispatch({ type: 'remove', key }),
      clearBasket: () => dispatch({ type: 'clear' }),
      clearBasketIfUnchanged: (fingerprint) => dispatch({ type: 'clearIfUnchanged', fingerprint }),
    }),
    [],
  );

  // Without useMemo this object would be rebuilt every render, and since consumers compare it
  // by reference, every consumer would re-render even when the basket had not changed.
  const value = useMemo(
    () => ({
      lines,
      itemCount: lines.reduce((count, l) => count + l.quantity, 0),
      subtotal: lines.reduce((sum, l) => sum + lineTotal(l), 0),
      ...actions, // spread copies the four stable functions in as properties of this object
    }),
    [lines, actions], // actions never changes, but listing it keeps the dependency list honest
  );

  return <BasketContext.Provider value={value}>{children}</BasketContext.Provider>;
}
