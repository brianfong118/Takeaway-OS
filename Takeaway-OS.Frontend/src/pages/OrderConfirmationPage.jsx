import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ORDER_STATUSES, ORDER_TYPES, getOrderByToken } from '../api/orders.js';
import { formatPrice, formatPriceDelta } from '../utils/format.js';
import { lineTotal } from '../utils/basket.js';
import './CheckoutPage.css';
import './OrderConfirmationPage.css';

// How often to re-ask the API for this order's status.
const POLL_MS = 5000;

// Nothing further will ever happen to an order in these states, so polling on is pure waste.
const TERMINAL_STATUSES = [ORDER_STATUSES.Completed, ORDER_STATUSES.Cancelled];

// A stuck Pending means the payment succeeded in the browser but no webhook ever arrived (Stripe
// outage, or the CLI forwarder not running in dev). Derived from createdAt rather than counting
// polls, so it stays correct across a refresh instead of restarting from zero.
const WEBHOOK_GRACE_MS = 120_000;

const COLLECTION_STEPS = [
  { status: ORDER_STATUSES.Paid, label: 'Payment confirmed' },
  { status: ORDER_STATUSES.Preparing, label: 'In the kitchen' },
  { status: ORDER_STATUSES.Ready, label: 'Ready to collect' },
  { status: ORDER_STATUSES.Completed, label: 'Collected' },
];

const DELIVERY_STEPS = [
  { status: ORDER_STATUSES.Paid, label: 'Payment confirmed' },
  { status: ORDER_STATUSES.Preparing, label: 'In the kitchen' },
  { status: ORDER_STATUSES.Ready, label: 'Ready for the driver' },
  { status: ORDER_STATUSES.OutForDelivery, label: 'Out for delivery' },
  { status: ORDER_STATUSES.Completed, label: 'Delivered' },
];

const HEADLINES = {
  [ORDER_STATUSES.Pending]: 'Confirming your payment',
  [ORDER_STATUSES.Paid]: 'Order confirmed',
  [ORDER_STATUSES.Preparing]: 'Your food is being made',
  [ORDER_STATUSES.Ready]: 'Ready',
  [ORDER_STATUSES.OutForDelivery]: 'On its way',
  [ORDER_STATUSES.Completed]: 'Order complete',
  [ORDER_STATUSES.Cancelled]: 'Order cancelled',
};

export default function OrderConfirmationPage() {
  // The whole page identity lives in the URL, so this survives a refresh, a bookmark, and a tab
  // close - none of which sessionStorage would have.
  const { token } = useParams();

  const [order, setOrder] = useState(null);
  const [error, setError] = useState(null);
  // Derived from a clock, so it cannot be computed during render: React may re-render at any time
  // and the result would change without the data changing (the react-hooks/purity rule). Recomputed
  // on each poll instead, which is a 5s granularity on a 2 minute threshold - far finer than needed.
  const [isWebhookLate, setIsWebhookLate] = useState(false);
  // Only ever true for the FIRST fetch. A background poll must not blank a page that already has
  // good data on it.
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let ignore = false;   // drops a response that arrives after unmount
    let intervalId;       // assigned just below; the poll callback needs it to stop itself

    async function load(isFirst) {
      try {
        const data = await getOrderByToken(token);
        if (ignore) return;

        setOrder(data);
        setError(null); // a recovered poll clears a stale failure message

        // An effect is allowed to read the clock; render is not. The API sends CreatedAt as UTC
        // with a Z suffix, so Date.parse needs no timezone handling of its own.
        setIsWebhookLate(
          data.status === ORDER_STATUSES.Pending &&
            Date.now() - Date.parse(data.createdAt) > WEBHOOK_GRACE_MS,
        );

        // Safe despite being assigned after the first load() call: everything up to the await
        // above runs synchronously, so setInterval has already assigned intervalId by the time
        // this line is reached.
        if (TERMINAL_STATUSES.includes(data.status)) clearInterval(intervalId);
      } catch (err) {
        if (ignore) return;

        // A failed FIRST load means there is nothing to render, so the error becomes the page.
        // A failed poll keeps the last good data on screen and simply tries again next tick -
        // one dropped request should not replace a working receipt with an error.
        if (isFirst) setError(err);
      } finally {
        // finally, not catch: unlike CheckoutPage nothing here navigates away on success, so this
        // component is still mounted either way.
        if (!ignore && isFirst) setIsLoading(false);
      }
    }

    load(true);
    intervalId = setInterval(() => load(false), POLL_MS);

    // Both halves are needed. clearInterval stops new requests being made; ignore discards one
    // already in flight. Without the clearInterval the timer outlives the page and keeps calling
    // setOrder on a component React has thrown away.
    return () => {
      ignore = true;
      clearInterval(intervalId);
    };
  }, [token]); // re-runs only if the URL changes to a different order

  if (isLoading) {
    return (
      <div className="checkout__empty">
        <h1>Your order</h1>
        <p>Loading your order...</p>
      </div>
    );
  }

  // Covers a mistyped link, a token from a wiped database, and a genuine network failure.
  if (error) {
    return (
      <div className="checkout__empty">
        <h1>Your order</h1>
        <p className="checkout__error" role="alert">
          {error.status === 404
            ? "We couldn't find that order. Please check the link, or call the restaurant to confirm it."
            : error.message}
        </p>
        <Link to="/" className="checkout__cta">Back to menu</Link>
      </div>
    );
  }

  const isDelivery = order.orderType === ORDER_TYPES.Delivery;
  const isCancelled = order.status === ORDER_STATUSES.Cancelled;
  const isPending = order.status === ORDER_STATUSES.Pending;

  const steps = isDelivery ? DELIVERY_STEPS : COLLECTION_STEPS;

  // -1 while Pending (the status isn't in either list), which correctly leaves every step unreached.
  const currentIndex = steps.findIndex((step) => step.status === order.status);

  return (
    <div className="confirmation">
      <h1 className="confirmation__title">{HEADLINES[order.status] ?? 'Your order'}</h1>

      {/* aria-live so a screen reader announces the status CHANGING under the customer, which is
          the whole point of the polling. Unlike role="alert" it doesn't interrupt on first render. */}
      <p className="confirmation__order-number" aria-live="polite">
        Order #{order.id}
      </p>

      {isPending && (
        <p className="confirmation__note">
          {isWebhookLate
            ? 'This is taking longer than usual. If your bank shows the payment went through, please call the restaurant to confirm your order.'
            : 'This updates by itself as soon as your bank confirms the payment - no need to reload.'}
        </p>
      )}

      {isCancelled ? (
        <p className="confirmation__note">
          This order was cancelled. If you did not expect that, please call the restaurant.
        </p>
      ) : (
        <ol className="confirmation__steps">
          {steps.map((step, index) => {
            // Comparing indexes, not statuses, is what makes a step light up for every status at or
            // past it rather than only its own.
            const isDone = currentIndex > index;
            const isCurrent = currentIndex === index;

            return (
              <li
                key={step.status}
                className={
                  isDone
                    ? 'confirmation__step confirmation__step--done'
                    : isCurrent
                      ? 'confirmation__step confirmation__step--current'
                      : 'confirmation__step'
                }
                // Tells assistive tech which one is the live position, rather than leaving the
                // distinction purely visual.
                aria-current={isCurrent ? 'step' : undefined}
              >
                {step.label}
              </li>
            );
          })}
        </ol>
      )}

      <section className="confirmation__panel">
        <h2>{isDelivery ? 'Delivering to' : 'Collection'}</h2>
        <p className="confirmation__detail">
          {isDelivery
            ? order.deliveryAddress
            : 'Collect from the restaurant - we will have it ready at the counter.'}
        </p>

        {/* Only rendered when the customer actually left a note, so the panel isn't mostly blank. */}
        {order.notes && (
          <>
            <h2>Your note</h2>
            <p className="confirmation__detail">{order.notes}</p>
          </>
        )}
      </section>

      <section className="confirmation__panel">
        <h2>What you ordered</h2>
        <ul className="checkout__lines">
          {order.items.map((item) => (
            <li key={item.id} className="checkout__line">
              <span className="checkout__line-name">
                {item.quantity} &times; {item.itemName}
                {item.modifiers.length > 0 && (
                  <span className="checkout__line-mods">
                    {/* The snapshotted name AND price from the moment of ordering, so this receipt
                        stays accurate even if the menu is repriced afterwards. */}
                    {item.modifiers
                      .map((m) => `${m.name} ${formatPriceDelta(m.priceDelta)}`.trim())
                      .join(', ')}
                  </span>
                )}
                {item.notes && <span className="checkout__line-mods">{item.notes}</span>}
              </span>
              <span>{formatPrice(lineTotal(item))}</span>
            </li>
          ))}
        </ul>

        {/* Broken out only when there was a fee, so a collection receipt stays a single line*/}
        {order.deliveryFee > 0 && (
          <>
            <div className="checkout__row">
              <span>Subtotal</span>
              {/* Derived from the server's own two figures rather than re-summing the lines,
                  so the three rows on screen always reconcile. */}
              <span>{formatPrice(order.total - order.deliveryFee)}</span>
            </div>
            <div className="checkout__row">
              <span>Delivery</span>
              <span>{formatPrice(order.deliveryFee)}</span>
            </div>
          </>
        )}

        <div className="checkout__subtotal">
          <span>Total</span>
          {/* The server's own figure, not a sum of the lines above - the same number Stripe charged. */}
          <span>{formatPrice(order.total)}</span>
        </div>
      </section>

      {/* The token is in the URL, so this really is all it takes to come back. Worth saying out
          loud, because a guest has no account and no other route to this order. */}
      <p className="confirmation__note">
        Bookmark this page to check your order later - the link is the only way back to it.
      </p>

      <Link to="/" className="checkout__cta">Order something else</Link>
    </div>
  );
}
