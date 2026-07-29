// Nothing secret lives here. A Stripe client secret only authorises paying THIS one
// PaymentIntent, which is why it is safe in the browser at all.
const KEY = 'takeawayos.pendingPayment';

export function savePendingPayment({ orderId, clientSecret }) {
  sessionStorage.setItem(KEY, JSON.stringify({ orderId, clientSecret }));
}

export function loadPendingPayment() {
  try {
    const stored = sessionStorage.getItem(KEY);
    if (!stored) return null;

    const parsed = JSON.parse(stored);
    // Guards a hand-edited or half-written entry: without both fields there is nothing to pay,
    // and treating it as absent is better than letting the payment page crash on undefined.
    return parsed?.orderId && parsed?.clientSecret ? parsed : null;
  } catch {
    return null; // same policy as the basket loader: corrupt storage reads as empty, never throws
  }
}

export function clearPendingPayment() {
  sessionStorage.removeItem(KEY);
}
