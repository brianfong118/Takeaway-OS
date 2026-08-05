import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth.js';
import { landingFor } from '../utils/routes.js';
import './AuthPage.css';

// Name and phone are here, not just email and password, because the API creates the Customer
// profile row in the SAME call as the login and RegisterRequest carries all four. Asking for them
// later would mean an account that exists but cannot yet have an order placed against it.
const EMPTY_FORM = { name: '', phone: '', email: '', password: '', confirmPassword: '' };

// Mirrors the Identity policy configured in Program.cs: RequiredLength = 8, and the four
// defaults left switched on. Written out because Identity's own rejection message arrives as
// one long sentence AFTER submitting
const PASSWORD_HINT =
  'At least 8 characters, including an uppercase letter, a lowercase letter, a number and a symbol.';

export default function RegisterPage() {
  const { isLoggedIn, user, signUp } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [form, setForm] = useState(EMPTY_FORM);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);

  // Same as LoginPage: set by ProtectedRoute if it bounced them, absent on a direct visit.
  const from = location.state?.from?.pathname ?? null;

  function handleChange(event) {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (isSubmitting) return;

    if (form.password !== form.confirmPassword) {
      setError(new Error('Those passwords do not match.'));
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      // Sends only what RegisterRequest has a field for , confirmPassword is browser-side
      const signedIn = await signUp({
        name: form.name.trim(),
        phone: form.phone.trim(),
        email: form.email.trim(),
        password: form.password, // NOT trimmed: spaces are legitimate password characters
      });

      navigate(from ?? landingFor(signedIn.role), { replace: true });
    } catch (err) {
      setError(err);
      setIsSubmitting(false); // not finally: the success path unmounts this component
    }
  }

  // Below the hooks, as in LoginPage: an early return above them would change the hook order.
  if (isLoggedIn) {
    return <Navigate to={from ?? landingFor(user.role)} replace />;
  }

  // Only complain once they have actually typed something into the second box. Comparing while
  // it is still empty would mark every new form as mismatched from the first keystroke.
  const mismatch = form.confirmPassword.length > 0 && form.password !== form.confirmPassword;

  return (
    <div className="auth">
      <h1 className="auth__title">Create an account</h1>

      {/* Identity's 400s (duplicate email, weak password) arrive as prose in the body, so
          err.message is already a sentence worth showing as-is. */}
      {error && <p className="auth__error" role="alert">{error.message}</p>}

      <form className="auth__form" onSubmit={handleSubmit}>
        <div className="auth__field">
          <label htmlFor="name">Name</label>
          <input
            id="name"
            name="name"
            type="text"
            required
            maxLength={100} // mirrors [MaxLength(100)] on the Customer profile
            autoComplete="name"
            autoFocus
            value={form.name}
            onChange={handleChange}
          />
        </div>

        <div className="auth__field">
          <label htmlFor="phone">Phone</label>
          <input
            id="phone"
            name="phone"
            type="tel" // changes the mobile keyboard only; applies no format validation
            required
            maxLength={30}
            autoComplete="tel"
            value={form.phone}
            onChange={handleChange}
          />
        </div>

        <div className="auth__field">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            required
            autoComplete="email"
            value={form.email}
            onChange={handleChange}
          />
        </div>

        <div className="auth__field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            required
            minLength={8} // mirrors options.Password.RequiredLength; the server is the real check
            // "new-password" (not "current-password" as on the login form) is what tells a
            // password manager to OFFER TO GENERATE one rather than fill a saved one in.
            autoComplete="new-password"
            aria-describedby="password-hint" // ties the hint to the field for a screen reader
            value={form.password}
            onChange={handleChange}
          />
          <p id="password-hint" className="auth__hint">{PASSWORD_HINT}</p>
        </div>

        <div className="auth__field">
          <label htmlFor="confirmPassword">Confirm password</label>
          <input
            id="confirmPassword"
            name="confirmPassword"
            type="password"
            required
            autoComplete="new-password"
            value={form.confirmPassword}
            onChange={handleChange}
          />
          {mismatch && (
            // aria-live rather than role="alert": this appears while they are still typing, and
            // an alert would interrupt a screen reader on every keystroke. Same call as the
            // checkout postcode warning.
            <p className="auth__mismatch" aria-live="polite">Those passwords do not match.</p>
          )}
        </div>

        <button type="submit" className="auth__submit" disabled={isSubmitting}>
          {isSubmitting ? 'Creating account...' : 'Create account'}
        </button>
      </form>

      <p className="auth__note">
        Already have an account? <Link to="/login">Sign in</Link>.
      </p>
      <p className="auth__note">
        You don’t need an account to order. <Link to="/">Browse the menu</Link> and check out as
        a guest.
      </p>
    </div>
  );
}
