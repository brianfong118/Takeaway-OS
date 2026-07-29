import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Elements } from '@stripe/react-stripe-js';
import PaymentForm from '../components/PaymentForm.jsx';
import { stripePromise } from '../utils/stripe.js';
import { loadPendingPayment, clearPendingPayment } from '../utils/pendingPayment.js';
import { formatPrice } from '../utils/format.js';
// Borrows the checkout stylesheet rather than duplicating it. Imported explicitly instead of
// relying on CheckoutPage having already pulled it in , that would break the day either page
// is code-split. Bundlers include a stylesheet once no matter how many modules import it.
import './CheckoutPage.css';
import './PaymentPage.css';

const APPEARANCE = {
  theme: 'stripe',
  variables: {
    colorPrimary: '#c8102e',
    colorText: '#1c1917',
    colorDanger: '#c0261c',
    fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
    borderRadius: '8px',
  },
};

export default function PaymentPage() {
  // Passing the FUNCTION, not calling it
  const [pending] = useState(loadPendingPayment);

  // { status, amount } once Stripe has been asked about this payment; null while that is in flight.
  const [intent, setIntent] = useState(null);
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    if (!pending) return; // nothing to look up; the early return further down covers the UI

    let ignore = false; // flipped by the cleanup below, to drop a response arriving after unmount

    async function load() {
      try {
        const stripe = await stripePromise; // resolves once Stripe.js has finished downloading

        // The payment's own record of itself, and this page's single source of truth. One call
        // covers three arrivals: fresh from checkout, back from a bank's 3D Secure redirect, and
        // a stale tab reopened after paying. A client secret grants access to exactly this one
        // PaymentIntent, which is why a publishable key is allowed to make the call at all.
        const { paymentIntent, error } = await stripe.retrievePaymentIntent(pending.clientSecret);
        if (ignore) return;

        if (error || !paymentIntent) {
          // Deliberately does not say "try again": if the secret is unreadable we cannot tell
          // whether money moved, so the phone is the only safe advice.
          setLoadError(
            'We could not check the status of this payment. Please call the restaurant to confirm your order before paying again.',
          );
          return;
        }

        // Already done -> drop the handoff now, so a later visit cannot re-offer a paid order.
        if (paymentIntent.status === 'succeeded') clearPendingPayment();

        // Stripe holds money as a whole number of pence (850 = £8.50). Divided once, for display
        // only. This is the server-computed total, and the only place the browser can learn it
        // from -> the basket was cleared the moment the order was created.
        setIntent({ status: paymentIntent.status, amount: paymentIntent.amount / 100 });
      } catch {
        if (!ignore) {
          setLoadError('We could not reach Stripe. Check your connection and reload this page.');
        }
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, [pending]); // `pending` is set once and never replaced, so this runs exactly once

  // Called by PaymentForm after a clean confirmation. does NOT tell the API.
  // The order is flipped to Paid by Stripe's webhook calling the server directly.
  function handleSuccess(paymentIntent) {
    clearPendingPayment();
    setIntent((current) => ({ ...current, status: paymentIntent.status })); 
  }

  // EARLY RETURNS (Every hook above runs unconditionally)
  // below all of them,
  // because React matches hooks to state by call order and a skipped call would shift the list.
  if (!pending) {
    return (
      <div className="checkout__empty">
        <h1>Payment</h1>
        <p>There is no order waiting to be paid for.</p>
        <Link to="/" className="checkout__cta">Browse the menu</Link>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="checkout__empty">
        <h1>Payment</h1>
        <p className="checkout__error" role="alert">{loadError}</p>
        <Link to="/" className="checkout__cta">Back to menu</Link>
      </div>
    );
  }

  if (!intent) {
    return (
      <div className="checkout__empty">
        <h1>Payment</h1>
        <p>Loading secure payment form...</p>
      </div>
    );
  }

  if (intent.status === 'succeeded') {
    return (
      <div className="checkout__empty">
        <h1>Payment received</h1>
        <p className="payment__amount">{formatPrice(intent.amount)} paid</p>
        <p>Order #{pending.orderId} is confirmed and the kitchen will start on it shortly.</p>
        <Link to="/" className="checkout__cta">Back to menu</Link>
      </div>
    );
  }

  // Some payment methods settle asynchronously and can sit here for minutes. The webhook marks
  // the order Paid whenever it lands, so there is nothing for the customer to do but wait.
  if (intent.status === 'processing') {
    return (
      <div className="checkout__empty">
        <h1>Payment processing</h1>
        <p>
          Your bank is still confirming the payment for order #{pending.orderId}. Nothing more is
          needed from you -&gt; the restaurant is notified as soon as it clears.
        </p>
        <Link to="/" className="checkout__cta">Back to menu</Link>
      </div>
    );
  }

  // Stripe or the bank killed this PaymentIntent. Its client secret is spent, so re-showing the
  // form would only produce an error -> a new order is the only way forward.
  if (intent.status === 'canceled') {
    return (
      <div className="checkout__empty">
        <h1>Payment cancelled</h1>
        <p>This payment was cancelled, and order #{pending.orderId} has not been paid for.</p>
        <Link to="/" className="checkout__cta">Start a new order</Link>
      </div>
    );
  }

  // Everything left (requires_payment_method, requires_confirmation, requires_action) means the
  // customer still has something to do, and all three are served by showing the form.
  return (
    <div className="payment">
      <h1 className="checkout__title">Payment</h1>
      <p className="payment__order">
        Order #{pending.orderId} is held for you and goes to the kitchen once it is paid for.
      </p>

      {/* The provider. It awaits stripePromise itself, then builds the iframes described by
          clientSecret, which is what tells Stripe which payment this form is for and how much it
          is. PaymentForm must be a child because it reads the context created here. */}
      <Elements
        stripe={stripePromise}
        options={{ clientSecret: pending.clientSecret, appearance: APPEARANCE }}
      >
        <PaymentForm amount={intent.amount} onSuccess={handleSuccess} />
      </Elements>

      <p className="payment__note">
        Card details go straight to Stripe and are never sent to this site.
      </p>

      {/* No "cancel payment" action. The order already exists server-side as Pending, and leaving
          it unpaid is exactly what an abandoned checkout looks like -> no cleanup call needed. */}
      <Link to="/" className="checkout__back">Back to menu</Link>
    </div>
  );
}
