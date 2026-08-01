import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth.js';
import { landingFor } from '../utils/routes.js';
import './LoginPage.css';

const EMPTY_FORM = { email: '', password: '' };

export default function LoginPage() {
  const { isLoggedIn, user, logIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [form, setForm] = useState(EMPTY_FORM);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);

  // Set by ProtectedRoute when it bounced them here. Absent on a direct visit, and gone after a
  // refresh, since it lives on the history entry rather than in the URL.
  // null rather than '/' so a direct visit can fall through to the role's landing page instead.
  const from = location.state?.from?.pathname ?? null;

  function handleChange(event) {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (isSubmitting) return;

    setIsSubmitting(true);
    setError(null);

    try {
      // logIn RETURNS the user: `user` from the hook still holds the old value for the rest of
      // this render, so the redirect has to read the return value, not state.
      const signedIn = await logIn(form.email.trim(), form.password);
      navigate(from ?? landingFor(signedIn.role), { replace: true }); // replace, so Back doesn't return to the login form
    } catch (err) {
      setError(err);
      setIsSubmitting(false); // not finally: the success path unmounts this component
    }
  }

  // Below the hooks: an early return above them would change the hook call order between renders.
  // Reached by visiting /login while already signed in, where `user` IS current.
  if (isLoggedIn) {
    return <Navigate to={from ?? landingFor(user.role)} replace />;
  }

  // The API sends no body with its 401, so client.js can only produce "Request failed with
  // status 401". Deliberately does not say WHICH of the two was wrong - that would confirm
  // to an attacker which email addresses have accounts.
  const message =
    error?.status === 401 ? 'That email and password combination is not recognised.' : error?.message;

  return (
    <div className="login">
      <h1 className="login__title">Sign in</h1>

      {error && <p className="login__error" role="alert">{message}</p>}

      <form className="login__form" onSubmit={handleSubmit}>
        <div className="login__field">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            required
            autoComplete="email"
            autoFocus
            value={form.email}
            onChange={handleChange}
          />
        </div>

        <div className="login__field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            required
            // "current-password" (not "new-password") is what tells a password manager this is a
            // sign-in, so it offers the saved credential instead of generating a fresh one.
            autoComplete="current-password"
            value={form.password}
            onChange={handleChange}
          />
        </div>

        <button type="submit" className="login__submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in...' : 'Sign in'}
        </button>
      </form>

      <p className="login__note">
        Ordering does not need an account. <Link to="/">Browse the menu</Link> and check out as a guest.
      </p>
    </div>
  );
}
