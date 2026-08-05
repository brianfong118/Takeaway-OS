import { useEffect, useState } from 'react';
import AccountNav from '../components/AccountNav.jsx';
import { getMyProfile, updateMyProfile } from '../api/customers.js';
import './AccountPage.css';

export default function AccountPage() {
  const [profile, setProfile] = useState(null);
  const [form, setForm] = useState({ name: '', phone: '' });
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  // Separate from `error` on purpose: a failed SAVE must not blank the profile already on
  // screen, so the two failures cannot share one slot
  const [saveError, setSaveError] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [justSaved, setJustSaved] = useState(false);

  useEffect(() => {
    let ignore = false; // drops a response that lands after this page has been left

    // The effect callback itself cannot be async (React reads its return value as the cleanup
    // function), hence the immediately-invoked async function inside.
    (async () => {
      try {
        const loaded = await getMyProfile();
        if (ignore) return;

        setProfile(loaded);
        // Seed the boxes from the server copy. This is the only place the two are synced from
        // the fetch; every later sync happens from the SAVE response instead.
        setForm({ name: loaded.name, phone: loaded.phone });
      } catch (err) {
        if (!ignore) setError(err);
      } finally {
        // No `if (ignore)` guard needed for React itself (it ignores a setState on an unmounted
        // component), but the flag is still checked above so a stale response cannot overwrite
        // a newer one.
        if (!ignore) setIsLoading(false);
      }
    })();

    return () => {
      ignore = true;
    };
  }, []);

  function handleChange(event) {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
    setJustSaved(false); // typing again means the "Saved" note no longer describes the boxes
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (isSaving) return;

    setIsSaving(true);
    setSaveError(null);

    try {
      // The API echoes the saved profile back, so there is no follow-up GET
      const saved = await updateMyProfile({ name: form.name.trim(), phone: form.phone.trim() });
      setProfile(saved);
      setForm({ name: saved.name, phone: saved.phone });
      setJustSaved(true);
    } catch (err) {
      setSaveError(err);
    } finally {
      // `finally` is right here, unlike the checkout form's catch-only: nothing navigates away
      // on success, so this component is still mounted either way and the button must re-enable.
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return (
      <div className="account">
        <h1 className="account__title">Your account</h1>
        <AccountNav />
        <p className="account__status">Loading your details...</p>
      </div>
    );
  }

  // A 404 here means a Customer-role login with no Customer row, which registration makes
  // impossible , but it is a real response, so it gets a real message rather than a crash.
  if (error) {
    return (
      <div className="account">
        <h1 className="account__title">Your account</h1>
        <AccountNav />
        <p className="account__error" role="alert">{error.message}</p>
      </div>
    );
  }

  // Both boxes non-empty and at least one of them different from the saved copy. Nothing to
  // save is a real state (they opened the page and touched nothing), and it reads better as a
  // disabled button than as a save that quietly does nothing.
  const hasChanges = form.name.trim() !== profile.name || form.phone.trim() !== profile.phone;

  return (
    <div className="account">
      <h1 className="account__title">Your account</h1>
      <AccountNav />

      {saveError && <p className="account__error" role="alert">{saveError.message}</p>}

      <form className="account__form" onSubmit={handleSubmit}>
        <div className="account__field">
          <label htmlFor="name">Name</label>
          <input
            id="name"
            name="name"
            type="text"
            required
            maxLength={100} // mirrors [MaxLength(100)] on CustomerProfileUpdateDto
            autoComplete="name"
            value={form.name}
            onChange={handleChange}
          />
        </div>

        <div className="account__field">
          <label htmlFor="phone">Phone</label>
          <input
            id="phone"
            name="phone"
            type="tel"
            required
            maxLength={30}
            autoComplete="tel"
            value={form.phone}
            onChange={handleChange}
          />
        </div>

        {/* Shown but not editable. Not a `disabled` input, which would grey the text out and
            imply it might become editable later: this is the Identity username the customer
            signs in with, so changing it is an auth concern (uniqueness, re-verification) and
            CustomerProfileUpdateDto deliberately has no field for it. A read-only line states
            that more honestly than a dead text box. */}
        <div className="account__field">
          <span className="account__label">Email</span>
          <p className="account__readonly">{profile.email}</p>
          <p className="account__hint">
            Your email is the address you sign in with and can’t be changed here.
          </p>
        </div>

        <div className="account__actions">
          <button type="submit" className="account__submit" disabled={isSaving || !hasChanges}>
            {isSaving ? 'Saving...' : 'Save changes'}
          </button>

          {/* aria-live so the confirmation is announced without stealing focus. It clears on
              the next keystroke rather than on a timer - a timer would be a second thing to
              clean up on unmount for no gain. */}
          {justSaved && <span className="account__saved" aria-live="polite">Saved.</span>}
        </div>
      </form>
    </div>
  );
}
