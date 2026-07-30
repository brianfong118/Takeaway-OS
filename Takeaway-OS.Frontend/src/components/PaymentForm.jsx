import { useState } from 'react';
import { PaymentElement, useStripe, useElements } from '@stripe/react-stripe-js';
import { formatPrice } from '../utils/format.js';

// onSuccess is a callback prop , the parent owns what "paid" means for the page, this component
// only knows the confirmation came back clean. It is handed the PaymentIntent so the parent can
// tell `succeeded` apart from `processing`.
export default function PaymentForm({ amount, onSuccess }) {
  // Both are null until stripePromise resolves, so every use of them is guarded.
  const stripe = useStripe();
  const elements = useElements();

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);

  async function handleSubmit(event) {
    event.preventDefault(); // cancel the native full-page form POST, same as CheckoutPage

    // Stripe.js not downloaded yet, or a second click landed while the first is in flight.
    // Two confirmations of one PaymentIntent is not a double charge (Stripe rejects the second),
    // but it does produce a confusing error, so it is cheaper to just not send it.
    if (!stripe || !elements || isSubmitting) return;

    setIsSubmitting(true);
    setError(null); // drop a previous decline so it can't be mistaken for the new attempt's result

    // no clientSecret argument. <Elements> was created with it (in paymentPage), and `elements` carries it
    // along with the customer's input, so the two can never drift apart.
    const { error: confirmError, paymentIntent } = await stripe.confirmPayment({
      elements,
      confirmParams: {
        // Only used if a redirect actually happens. It has to be an absolute URL, and one that
        // works cold: /pay rebuilds itself from sessionStorage, so a returning customer is fine.
        return_url: `${window.location.origin}/pay`,
      },
      // Keep plain cards (and modal 3D Secure) on this page. If a redirect IS required the
      // browser leaves and nothing below this line runs , the return trip re-mounts PaymentPage.
      // Stripe appends ?payment_intent_client_secret=... to return_url, but nothing reads it:
      // <Elements> does not look at the URL. PaymentPage recovers the same secret from
      // sessionStorage on mount and asks Stripe for the status itself.
      redirect: 'if_required',
    });

    // Resolves rather than throws, so this is an if, not a catch. Exactly one of
    // confirmError / paymentIntent is populated.
    if (confirmError) {
      // These two types are the ones Stripe.js renders INSIDE the PaymentElement itself, and that
      // display is not ours to turn off - it lives in a cross-origin iframe. 
      const isShownInlineByStripe =
        confirmError.type === 'card_error' || confirmError.type === 'validation_error';

      // A failed 3D Secure check arrives as invalid_request_error (NOT card_error), so Stripe does
      // NOT show it inline , our banner is the only message the customer would get.
      const isFailedAuthentication =
        confirmError.code === 'payment_intent_authentication_failure';

      // null clears the banner rather than leaving a previous one up. Every branch that reaches
      // here has a message somewhere on screen: inline from Stripe, or in the banner below.
      setError(
        isShownInlineByStripe
          ? null
          : isFailedAuthentication
            ? 'Your bank could not verify this card. Please try a different card.'
            : 'The payment could not be completed. Please try again.',
      );
      setIsSubmitting(false); // stay on the form: a decline is retryable with another card
      return;
    }

    // Deliberately does NOT tell the API anything. The order is marked Paid by Stripe's webhook
    // calling the server directly , a browser claiming success can be forged or simply lost.
    onSuccess(paymentIntent);
  }

  return (
    <form className="payment__form" onSubmit={handleSubmit}>
      {/* No value/onChange: unlike the checkout inputs this is not controlled by React state.
          It renders iframes from js.stripe.com, so the card number never enters this app's
          JavaScript, this app's memory, or any request our code makes. */}
      <PaymentElement />

      {error && <p className="checkout__error" role="alert">{error}</p>}

      <button
        type="submit"
        className="payment__submit"
        disabled={!stripe || isSubmitting} // stays dead until Stripe.js has actually loaded
      >
        {isSubmitting ? 'Paying...' : `Pay ${formatPrice(amount)}`}
      </button>
    </form>
  );
}
