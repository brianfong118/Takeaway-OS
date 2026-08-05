import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ORDER_TYPES, createOrder } from '../api/orders.js';
import { getRestaurantStatus } from '../api/openingHours.js';
import { getSettings } from '../api/settings.js';
import { getDeliveryAreas } from '../api/deliveryAreas.js';
import { getMyProfile } from '../api/customers.js';
import { getMyAddresses } from '../api/addresses.js';
import { ROLES } from '../api/auth.js';
import { useAuth } from '../hooks/useAuth.js';
import { useBasket } from '../hooks/useBasket.js';
import { savePendingPayment } from '../utils/pendingPayment.js';
import { formatPrice } from '../utils/format.js';
import { lineTotal } from '../utils/basket.js';
import { looksOutsideArea } from '../utils/postcode.js';
import './CheckoutPage.css';

// One object rather than five useState calls, so handleChange below can stay generic.
// Collection is the default because it needs no address 
const EMPTY_FORM = {
  customerName: '',
  customerPhone: '',
  deliveryAddress: '',
  deliveryPostcode: '',
  orderType: ORDER_TYPES.Collection,
  notes: '',
};

export default function CheckoutPage() {
  const { lines, subtotal, clearBasket } = useBasket();
  const { user } = useAuth();
  const navigate = useNavigate();

  const [form, setForm] = useState(EMPTY_FORM);
  const [status, setStatus] = useState(null); // RestaurantStatusDto, or null while unknown
  const [deliveryFee, setDeliveryFee] = useState(null); // null = not known yet, NOT "free"
  const [areas, setAreas] = useState([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);

  // The customer's saved addresses, and which one is picked. null = "type a new one", which is
  // also what a guest is permanently on. Kept OUT of `form`: everything in there is a text box
  // the customer fills in, whereas this is a choice between an id and free text, and mixing the
  // two would mean the submit handler could not tell which of them to believe.
  const [addresses, setAddresses] = useState([]);
  const [selectedAddressId, setSelectedAddressId] = useState(null);

  // only a Customer-role token can read /api/customers/me and /api/addresses, so this
  // decides whether those two requests are worth making at all.
  const isCustomer = user?.role === ROLES.Customer;

  useEffect(() => {
    let ignore = false; // flipped by the cleanup below, to drop a response arriving after unmount

    async function load() {
      // allSettled, not all: Promise.all rejects as soon as either request does, so one dead
      // endpoint would cost us BOTH answers. Here each is independently useful.
      const [statusResult, feeResult, areasResult] = await Promise.allSettled([
        getRestaurantStatus(),
        getSettings(),
        getDeliveryAreas(),
      ]);

      if (ignore) return;

      // A failed status check must not block checkout
      // OrderService re-runs the exact same check on submit.
      // Leaving status null allows customer to try
      if (statusResult.status === 'fulfilled') setStatus(statusResult.value);

      // Left null on failure, which hides the Total rather than showing one with the fee
      // silently missing. Quoting a number lower than what Stripe then charges is the worse
      // outcome by far, so "no total" is the safer thing to render.
      if (feeResult.status === 'fulfilled') setDeliveryFee(feeResult.value.deliveryFee);

      // Left as [] on failure, which silently drops the hint and the warning. Unlike the fee,
      // this one costs nothing to lose: it is advisory, and the server still refuses an
      // out-of-area order. Nothing here should block checkout on a failed courtesy fetch.
      if (areasResult.status === 'fulfilled') setAreas(areasResult.value);
    }

    load();

    return () => {
      ignore = true;
    };
  }, []); // empty dependency array -> runs once on mount; neither value depends on a prop

  // A SECOND effect rather than more entries in the one above, because these two requests are
  // conditional and the other three are not. Folding them in would have meant either building
  // the array dynamically (and losing the positional destructuring that makes it readable) or
  // firing two requests that can only 401 for a guest. Two effects both run on mount, so this
  // costs nothing in speed - they are still in flight at the same time.
  useEffect(() => {
    if (!isCustomer) return; // a guest has nothing to prefill and no saved addresses

    let ignore = false;

    (async () => {
      const [profileResult, addressResult] = await Promise.allSettled([
        getMyProfile(),
        getMyAddresses(),
      ]);

      if (ignore) return;

      if (profileResult.status === 'fulfilled') {
        // `current.customerName ||` - prefill only what is still EMPTY.
        setForm((current) => ({
          ...current,
          customerName: current.customerName || profileResult.value.name,
          customerPhone: current.customerPhone || profileResult.value.phone,
        }));
      }

      if (addressResult.status === 'fulfilled') {
        setAddresses(addressResult.value);
        if (addressResult.value.length > 0) setSelectedAddressId(addressResult.value[0].id);
      }
    })();

    return () => {
      ignore = true;
    };
  }, [isCustomer]);

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

    // Exactly one of the two address sources is sent, never both
    const usingSavedAddress = isDelivery && selectedAddressId !== null;

    try {
      const response = await createOrder({
        customerName: form.customerName.trim(),
        customerPhone: form.customerPhone.trim(),
        // Blanked for collection so a half-typed address can't be filed against an order
        // nobody is delivering. The API's IValidatableObject only requires it for Delivery,
        // and accepts it from EITHER this field or addressId below.
        deliveryAddress: isDelivery && !usingSavedAddress ? form.deliveryAddress.trim() : '',
        // Blanked for collection on the same reasoning as the address.
        deliveryPostcode: isDelivery && !usingSavedAddress ? form.deliveryPostcode.trim() : '',
        // The saved address's OWN postcode is what the server will check the delivery area against
        addressId: usingSavedAddress ? selectedAddressId : null,
        orderType: form.orderType,
        notes: form.notes.trim(),
        lines,
      });

      // Order before basket: if writing the handoff somehow threw, the basket would still be
      // intact and the customer could retry, rather than losing it with nothing to show for it.
      savePendingPayment({
        orderId: response.order.id,
        clientSecret: response.clientSecret,
        // Sent by the API only in this one response. A guest has no login, so this token is the
        // sole route back to their own order -> losing it here loses it permanently.
        publicToken: response.publicToken,
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

  // Collection is 0 by definition, so it never has to wait on the settings fetch. Delivery
  // stays null until the fee is known, which is what propagates into `total` below.
  const appliedFee = isDelivery ? deliveryFee : 0;

  // Mirrors OrderService.ComputeTotal's last term: the fee is added ONCE, per order, here at
  // the summary. It must never go through lineTotal, which would multiply it by the lines.
  const total = appliedFee === null ? null : subtotal + appliedFee;

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

  // Recomputed on every render rather than stored in state alongside the id: two pieces of
  // state that must agree can disagree, and this one is derivable from the other.
  const savedAddress = addresses.find((a) => a.id === selectedAddressId) ?? null;

  // A saved address's postcode is already normalised by the server, and the typed one is whatever is in the box right now.
  const postcodeToCheck = savedAddress ? savedAddress.postcode : form.deliveryPostcode;

  // Advisory only. It deliberately does NOT feed the button's `disabled`, unlike isClosed above:
  // a disabled button would make the server's own refusal unreachable through the UI, and the
  // server is the thing that actually decides. The customer can always press on and be told.
  const outsideArea = isDelivery && looksOutsideArea(postcodeToCheck, areas);

  const showAddressPicker = isDelivery && addresses.length > 0;

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

      {!user && (
        <p className="checkout__account">
          <Link to="/login">Sign in</Link> or <Link to="/register">create an account</Link> to
          save your details for next time — or just carry on below as a guest.
        </p>
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

        {/* Only for a signed-in customer with at least one saved address. Everyone else falls
            straight through to the free-text fields below, unchanged. */}
        {showAddressPicker && (
          <div className="checkout__field">
            <label htmlFor="savedAddress">Deliver to</label>
            <select
              id="savedAddress"
              name="savedAddress"
              // ?? '' because null would make this an UNCONTROLLED select, which React then
              // refuses to keep in sync. '' is a real option here -> the "different address" one.
              value={selectedAddressId ?? ''}
              onChange={(event) => {
                // e.target.value is ALWAYS a string, even for numeric option values, so the id
                // has to go back through Number() before it can match an address's numeric id.
                const { value } = event.target;
                setSelectedAddressId(value === '' ? null : Number(value));
              }}
            >
              {addresses.map((address) => (
                <option key={address.id} value={address.id}>
                  {/* Label first when they gave one*/}
                  {address.label ? `${address.label} — ` : ''}
                  {address.line1}, {address.postcode}
                </option>
              ))}
              <option value="">Use a different address</option>
            </select>

            <p className="checkout__hint">
              <Link to="/account/addresses">Manage your saved addresses</Link>
            </p>
          </div>
        )}

        {/* Rendered only for delivery, and only when a saved address ISN'T being used. Because
            they are removed from the tree entirely, their `required` goes with them */}
        {isDelivery && !savedAddress && (
          <div className="checkout__field">
            <label htmlFor="deliveryAddress">Delivery address</label>
            <textarea
              id="deliveryAddress"
              name="deliveryAddress"
              rows={3}
              required
              maxLength={250}
              autoComplete="street-address"
              placeholder="House number, street, town"
              value={form.deliveryAddress}
              onChange={handleChange}
            />
          </div>
        )}

        {/* Its own field rather than part of the address, because this is the value the
            delivery-area check runs on. Removed from the tree for collection*/}
        {isDelivery && !savedAddress && (
          <div className="checkout__field">
            <label htmlFor="deliveryPostcode">Postcode</label>
            <input
              id="deliveryPostcode"
              name="deliveryPostcode"
              type="text"
              required
              maxLength={10} // mirrors [MaxLength(10)] on OrderCreateDto
              autoComplete="postal-code"
              placeholder="E1 6AN"
              className="checkout__postcode"
              value={form.deliveryPostcode}
              onChange={handleChange}
            />

            {/* Rendered only when the list actually loaded, so a failed fetch shows nothing
                rather than an empty "We deliver to:". */}
            {areas.length > 0 && (
              <p className="checkout__hint">
                We deliver to {areas.map((a) => a.outwardCode).join(', ')}.
              </p>
            )}
          </div>
        )}

        {outsideArea && (
          // aria-live rather than role="alert": this appears while the customer is still
          // typing, and an alert would interrupt a screen reader mid-word on every keystroke.
          <p className="checkout__warning" aria-live="polite">
            We don’t currently deliver to that postcode. You can still place a collection order.
          </p>
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

          {/* Only broken out for delivery. On collection the subtotal IS the total, and two
              rows showing the same number reads as a bug rather than as clarity. */}
          {isDelivery && (
            <>
              <div className="checkout__row">
                <span>Subtotal</span>
                <span>{formatPrice(subtotal)}</span>
              </div>
              {deliveryFee !== null && (
                <div className="checkout__row">
                  <span>Delivery</span>
                  <span>{formatPrice(deliveryFee)}</span>
                </div>
              )}
            </>
          )}

          {total === null ? (
            <p className="checkout__disclaimer">
              We could not load the delivery charge just now. Your total will be confirmed when
              you place the order.
            </p>
          ) : (
            <div className="checkout__subtotal">
              <span>Total</span>
              <span>{formatPrice(total)}</span>
            </div>
          )}

          {/* the server prices the order from the live menu, so a repricing while the basket
              sat open can still move the figure. */}
          <p className="checkout__disclaimer">
            Prices are taken from the live menu when you place the order.
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
