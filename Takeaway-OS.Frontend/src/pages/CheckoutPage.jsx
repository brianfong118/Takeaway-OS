import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ORDER_TYPES, createOrder } from '../api/orders.js';
import { getRestaurantStatus } from '../api/openingHours.js';
import { useBasket } from '../hooks/useBasket.js';
import { savePendingPayment } from '../utils/pendingPayment.js';
import { formatPrice } from '../utils/format.js';
import { lineTotal } from '../utils/basket.js';
import './CheckoutPage.css';

// One object rather than five useState calls, so handleChange below can stay generic.
// Collection is the default because it needs no address 
const EMPTY_FORM = {
  customerName: '',
  customerPhone: '',
  deliveryAddress: '',
  orderType: ORDER_TYPES.Collection,
  notes: '',
};

export default function CheckoutPage() {
  const { lines, subtotal, clearBasket } = useBasket();
  const navigate = useNavigate();

  const [form, setForm] = useState(EMPTY_FORM);
  const [status, setStatus] = useState(null); // RestaurantStatusDto, or null while unknown
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let ignore = false; // flipped by the cleanup below, to drop a response arriving after unmount

    async function load() {
      try {
        const data = await getRestaurantStatus();
        if (!ignore) setStatus(data);
      } catch {
        // A failed status check must not block checkout
        // OrderService re-runs the exact same check on submit. 
        // Leaving status null allows customer to try
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, []); // empty dependency array -> runs once on mount; the status doesn't depend on any prop

  // event.target is the DOM element that fired the change; its `name` matches a key in the form
  // object, so one handler serves every field instead of one closure per input.
  function handleChange(event) {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
  }

  async function handleSubmit(event) {
    // A <form> submit natively does a full-page POST-and-reload, which would destroy all React
    // state. Cancelling it is what turns the form into an ordinary async function call.
    event.preventDefault();

    if (isSubmitting) return; // double-click guard: two POSTs would be two real orders, two charges

    setIsSubmitting(true);
    setError(null); // clear a previous failure so the old message can't look like a new one

    const isDelivery = form.orderType === ORDER_TYPES.Delivery;

    try {
      const response = await createOrder({
        customerName: form.customerName.trim(),
        customerPhone: form.customerPhone.trim(),
        // Blanked for collection so a half-typed address can't be filed against an order
        // nobody is delivering. The API's IValidatableObject only requires it for Delivery.
        deliveryAddress: isDelivery ? form.deliveryAddress.trim() : '',
        addressId: null, // saved addresses arrive with accounts; guest checkout always sends free text
        orderType: form.orderType,
        notes: form.notes.trim(),
        lines,
      });

      // Order before basket: if writing the handoff somehow threw, the basket would still be
      // intact and the customer could retry, rather than losing it with nothing to show for it.
      savePendingPayment({
        orderId: response.order.id,
        clientSecret: response.clientSecret,
      });
      clearBasket();
      navigate('/pay');
    } catch (err) {
      setError(err);
      // Reset here rather than in a `finally`: the success path navigates away, so re-enabling
      // the button there would only touch a component that is already gone.
      setIsSubmitting(false);
    }
  }

  // Every hook above runs unconditionally , the early return has to sit below them, because
  // React matches hooks to state by call order and a skipped call would shift the whole list.
  if (lines.length === 0) {
    return (
      <div className="checkout__empty">
        <h1>Checkout</h1>
        <p>Your basket is empty, so there is nothing to check out.</p>
        <Link to="/" className="checkout__cta">Browse the menu</Link>
      </div>
    );
  }

  const isDelivery = form.orderType === ORDER_TYPES.Delivery;

  // Two independent ways to learn the shop is shut: the status fetch on mount, and a 409 from
  // the submit itself (the shop can close in between). 
  // The 409 is listed first because it is the fresher fact and it has to feed `disabled` too, 
  // or the button stays invitingly red after a rejection and the customer just gets a second one.
  // Nothing clears this short of revisiting the page, which is the correct outcome: the only
  // thing that would make the order succeed is the shop reopening.
  const isClosed = error?.status === 409 || (status !== null && !status.isOpen);

  // The fallback covers only the case where the API sends an empty message.
  const closedMessage =
    (error?.status === 409 ? error.message : status?.message) || 'We’re closed right now.';

  return (
    <div className="checkout">
      <h1 className="checkout__title">Checkout</h1>

      {isClosed && (
        // role="alert" makes a screen reader announce this the moment it appears, which matters
        // for the 409 case since it shows up only after the customer presses Place order.
        <div className="checkout__banner" role="alert">{closedMessage}</div>
      )}

      {/* A validation failure, a network error, anything that is not the closed case.
          409 is excluded so it isn't reported twice, in two different styles. */}
      {error && error.status !== 409 && (
        <p className="checkout__error" role="alert">{error.message}</p>
      )}

      {/* onSubmit on the <form>, not onClick on the button, so the Enter key inside any field
          submits too — that is native browser behaviour a click handler alone would not get. */}
      <form className="checkout__form" onSubmit={handleSubmit}>
        <fieldset className="checkout__group">
          <legend>How do you want it?</legend>

          {/* Radios share one `name`, which is what makes the browser treat them as one
              mutually-exclusive group. `checked` is derived from state, so state is the
              single source of truth and the DOM only reflects it. */}
          <label className="checkout__radio">
            <input
              type="radio"
              name="orderType"
              value={ORDER_TYPES.Collection}
              checked={!isDelivery}
              onChange={handleChange}
            />
            <span>Collection</span>
          </label>

          <label className="checkout__radio">
            <input
              type="radio"
              name="orderType"
              value={ORDER_TYPES.Delivery}
              checked={isDelivery}
              onChange={handleChange}
            />
            <span>Delivery</span>
          </label>
        </fieldset>

        <div className="checkout__field">
          <label htmlFor="customerName">Name</label>
          <input
            id="customerName"
            name="customerName" // must match the form state key handleChange writes to
            type="text"
            required // browser blocks submit and focuses the field; the API's [Required] is the real check
            maxLength={100} // mirrors [MaxLength(100)] so the limit is felt while typing, not after a 400
            autoComplete="name"
            value={form.customerName}
            onChange={handleChange}
          />
        </div>

        <div className="checkout__field">
          <label htmlFor="customerPhone">Phone</label>
          <input
            id="customerPhone"
            name="customerPhone"
            // type="tel" only changes the on-screen keyboard on mobile; it applies no format
            // validation, which matches the API deliberately not using [Phone].
            type="tel"
            required
            maxLength={30}
            autoComplete="tel"
            value={form.customerPhone}
            onChange={handleChange}
          />
        </div>

        {/* Rendered only for delivery. Because it is removed from the tree entirely, its
            `required` disappears with it , a collection order can't be blocked by a field
            the customer cannot see. */}
        {isDelivery && (
          <div className="checkout__field">
            <label htmlFor="deliveryAddress">Delivery address</label>
            <textarea
              id="deliveryAddress"
              name="deliveryAddress"
              rows={3}
              required
              maxLength={250}
              autoComplete="street-address"
              placeholder="House number, street, postcode"
              value={form.deliveryAddress}
              onChange={handleChange}
            />
          </div>
        )}

        <div className="checkout__field">
          <label htmlFor="notes">Notes for the kitchen (optional)</label>
          <textarea
            id="notes"
            name="notes"
            rows={2}
            maxLength={500}
            placeholder="e.g. ring the bell, allergic to nuts"
            value={form.notes}
            onChange={handleChange}
          />
        </div>

        <section className="checkout__summary">
          <h2>Your order</h2>
          <ul className="checkout__lines">
            {lines.map((line) => (
              <li key={line.key} className="checkout__line">
                <span className="checkout__line-name">
                  {line.quantity} &times; {line.name}
                  {line.modifiers.length > 0 && (
                    <span className="checkout__line-mods">
                      {line.modifiers.map((m) => m.name).join(', ')}
                    </span>
                  )}
                </span>
                <span>{formatPrice(lineTotal(line))}</span>
              </li>
            ))}
          </ul>

          <div className="checkout__subtotal">
            <span>Subtotal</span>
            <span>{formatPrice(subtotal)}</span>
          </div>
          <p className="checkout__disclaimer">
            The final amount is calculated by the restaurant when the order is placed.
          </p>
        </section>

        <button
          type="submit" // the type that triggers onSubmit; the default for a button inside a form
          className="checkout__submit"
          disabled={isSubmitting || isClosed}
        >
          {isSubmitting ? 'Placing order...' : 'Place order'}
        </button>

        <Link to="/basket" className="checkout__back">Back to basket</Link>
      </form>
    </div>
  );
}
