import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth.js';
import './LoginPage.css';

const EMPTY_FORM = { email: '', password: '' };

export default function LoginPage() {
  const { isLoggedIn, logIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [form, setForm] = useState(EMPTY_FORM);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);

  // Set by ProtectedRoute when it bounced them here. Absent on a direct visit, and gone after a
  // refresh, since it lives on the history entry rather than in the URL.
  const from = location.state?.from?.pathname ?? '/';

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
      await logIn(form.email.trim(), form.password);
      navigate(from, { replace: true }); // replace, so Back doesn't return to the login form
    } catch (err) {
      setError(err);
      setIsSubmitting(false); // not finally: the success path unmounts this component
    }
  }

  // Below the hooks: an early return above them would change the hook call order between renders.
  if (isLoggedIn) {
    return <Navigate to={from} replace />;
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
