import { Link } from 'react-router-dom';
import { loadPendingPayment } from '../utils/pendingPayment.js';
// Borrows the checkout stylesheet rather than duplicating it. Imported explicitly instead of
// relying on CheckoutPage having already pulled it in , that would break the day either page
// is code-split. Bundlers include a stylesheet once no matter how many modules import it.
import './CheckoutPage.css';

// STUB — step 5 replaces the body of this page with the Stripe payment form, using the
// clientSecret read below. It exists now so the checkout flow can be walked end to end:
// without a route at /pay, a successful order would land on the 404 page.
export default function PaymentPage() {
  const pending = loadPendingPayment();

  // No handoff in sessionStorage: either nothing was ordered in this tab, or the payment has
  // already been completed and cleared. Either way there is nothing to pay for here.
  if (!pending) {
    return (
      <div className="checkout__empty">
        <h1>Payment</h1>
        <p>There is no order waiting to be paid for.</p>
        <Link to="/" className="checkout__cta">Browse the menu</Link>
      </div>
    );
  }

  return (
    <div className="checkout__empty">
      <h1>Order #{pending.orderId} placed</h1>
      <p>
        Your order is saved and waiting for payment. The card form goes here next.
      </p>
      <Link to="/" className="checkout__cta">Back to menu</Link>
    </div>
  );
}
