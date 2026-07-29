import { Link } from 'react-router-dom';
import { useBasket } from '../hooks/useBasket.js';
import { MAX_LINE_QUANTITY, lineTotal } from '../utils/basket.js';
import { formatPrice, formatPriceDelta } from '../utils/format.js';
import './BasketPage.css';

export default function BasketPage() {
  const { lines, subtotal, setLineQuantity, removeLine, clearBasket } = useBasket();

  if (lines.length === 0) {
    return (
      <div className="basket__empty">
        <h1>Your basket</h1>
        <p>Nothing in it yet.</p>
        <Link to="/" className="basket__cta">Browse the menu</Link>
      </div>
    );
  }

  return (
    <div className="basket">
      <h1 className="basket__title">Your basket</h1>

      <ul className="basket__lines">
        {lines.map((line) => (
          // line.key is content-derived and stable, so it identifies the row across renders.
          <li key={line.key} className="basket__line">
            <div className="basket__line-main">
              <h2 className="basket__line-name">{line.name}</h2>

              {line.modifiers.length > 0 && (
                <ul className="basket__modifiers">
                  {line.modifiers.map((modifier) => (
                    <li key={modifier.id}>
                      {modifier.name} {formatPriceDelta(modifier.priceDelta)}
                    </li>
                  ))}
                </ul>
              )}

              {line.notes && <p className="basket__note">&ldquo;{line.notes}&rdquo;</p>}
            </div>

            <div className="basket__line-side">
              <span className="basket__line-total">{formatPrice(lineTotal(line))}</span>

              <div className="basket__quantity">
                <button
                  type="button"
                  onClick={() => setLineQuantity(line.key, line.quantity - 1)}
                  disabled={line.quantity <= 1} // Remove is the way to reach zero
                  aria-label={`Decrease quantity of ${line.name}`}
                >
                  -
                </button>
                <span aria-live="polite">{line.quantity}</span>
                <button
                  type="button"
                  onClick={() => setLineQuantity(line.key, line.quantity + 1)}
                  disabled={line.quantity >= MAX_LINE_QUANTITY}
                  aria-label={`Increase quantity of ${line.name}`}
                >
                  +
                </button>
              </div>

              <button
                type="button"
                className="basket__remove"
                onClick={() => removeLine(line.key)}
              >
                Remove
              </button>
            </div>
          </li>
        ))}
      </ul>

      <div className="basket__summary">
        <div className="basket__subtotal">
          <span>Subtotal</span>
          <span>{formatPrice(subtotal)}</span>
        </div>

        {/* Says subtotal, not total: the server recalculates from current menu prices when the
            order is placed, and that figure is what gets charged. */}
        <p className="basket__disclaimer">Confirmed when you check out.</p>

        {/* A Link, not a button: checkout is a destination, so it should be a real anchor the
            customer can middle-click or open in a new tab. Styled to look like the button it
            replaces. */}
        <Link to="/checkout" className="basket__checkout">
          Checkout
        </Link>

        <button type="button" className="basket__clear" onClick={clearBasket}>
          Clear basket
        </button>
      </div>
    </div>
  );
}
