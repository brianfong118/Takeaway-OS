import { loadStripe } from '@stripe/stripe-js';

// Publishable, not secret -> baked into the bundle every visitor downloads, and that is fine:
// it can only *start* payment attempts against PaymentIntents that already exist. Creating them,
// refunding, and reading anything back all need the secret key, which never leaves the API.
const PUBLISHABLE_KEY = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY;

// Same fail-at-startup policy as client.js. A missing key would otherwise surface as an opaque
// Stripe error at the moment a customer tries to pay , the worst possible time to find out.
// It does mean a missing key takes the whole app down, not just /pay. Deliberate: in dev that is
// a one-line fix, and in production the key is present or the shop cannot take money at all.
if (!PUBLISHABLE_KEY) {
  throw new Error(
    'VITE_STRIPE_PUBLISHABLE_KEY is not set. Add it to .env.local and restart the dev server.',
  );
}

// Called here at module scope so it runs exactly once per page load: ES modules are cached after
// their first import, so every file importing this gets the same pending promise. Calling it
// inside a component instead would re-download Stripe.js on every single render.
// Not awaited , <Elements> takes the unresolved promise and waits on it itself.
export const stripePromise = loadStripe(PUBLISHABLE_KEY);
