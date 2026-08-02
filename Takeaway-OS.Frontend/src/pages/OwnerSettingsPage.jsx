import { useEffect, useState } from 'react';
import {
  getSchedule,
  getRestaurantStatus,
  createWindow,
  updateWindow,
  deleteWindow,
  setClosure,
} from '../api/openingHours.js';
import { groupByDay, toTimeInput, fromTimeInput, describeWindow } from '../utils/hours.js';
import OwnerNav from '../components/OwnerNav.jsx';
import './OwnerSettingsPage.css';

export default function OwnerSettingsPage() {
  const [schedule, setSchedule] = useState([]);
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [windows, currentStatus] = await Promise.all([getSchedule(), getRestaurantStatus()]);
        if (!active) return;
        setSchedule(windows);
        setStatus(currentStatus);
      } catch (err) {
        if (active) setError(err.message);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  if (loading) {
    return (
      <div className="settings">
        <OwnerNav />
        <p className="settings__message">Loading settings...</p>
      </div>
    );
  }

  return (
    <div className="settings">
      <OwnerNav />
      <h1 className="settings__title">Settings</h1>

      {error && (
        <p className="settings__error" role="alert">
          {error}
        </p>
      )}

      <ClosureCard status={status} onStatusChange={setStatus} />

      <ScheduleCard
        schedule={schedule}
        onScheduleChange={setSchedule}
        // The banner customers see is derived from the schedule, so a schedule edit can change
        // it. Refetching the same endpoint OrderService checks is what keeps this page honest
        // rather than recomputing open/closed in the browser.
        onRefreshStatus={() => getRestaurantStatus().then(setStatus).catch(() => {})}
      />
    </div>
  );
}

// RestaurantStatusDto.Message is EMPTY while the shop is open: it exists to explain a closure to
// a customer, and an open shop has nothing to explain. The customer's banner only renders when
// closed, so it never noticed. This readout is always on screen, so it supplies its own wording
// for the open case rather than rendering an empty pill.
function describeStatus(status) {
  if (!status) return 'Status unavailable';
  if (status.isOpen) return 'Open for orders';
  return status.message || 'Closed';
}

function ClosureCard({ status, onStatusChange }) {
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  // The status endpoint reports open/closed, not WHY. A closure the owner set is inferred from
  // the message rather than exposed as a flag, so the toggle reflects what customers are told.
  const isClosed = status ? !status.isOpen : false;

  async function apply(isTemporarilyClosed) {
    setBusy(true);
    setError(null);
    try {
      // Returns the resulting RestaurantStatusDto, so the owner immediately sees the customer's
      // view instead of a stale local guess.
      const next = await setClosure({
        isTemporarilyClosed,
        closureReason: isTemporarilyClosed ? reason.trim() : '',
      });
      onStatusChange(next);
      if (!isTemporarilyClosed) setReason('');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="settings__card">
      <h2 className="settings__card-title">Temporary closure</h2>
      <p className="settings__hint">
        Shuts ordering for a one-off holiday without touching the weekly schedule. It stays on
        until you turn it off, so remember to reopen.
      </p>

      <p className={`closure__status ${status?.isOpen ? 'closure__status--open' : 'closure__status--shut'}`}>
        <span className="closure__dot" aria-hidden="true" />
        {describeStatus(status)}
      </p>

      {error && (
        <p className="settings__error" role="alert">
          {error}
        </p>
      )}

      <div className="closure__controls">
        <label className="closure__field">
          <span className="settings__label">Reason (shown to customers)</span>
          <input
            className="settings__input"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Closed for Christmas"
            maxLength={200}
            disabled={busy || isClosed}
          />
        </label>

        {isClosed ? (
          <button type="button" className="settings__primary" disabled={busy} onClick={() => apply(false)}>
            {busy ? 'Reopening...' : 'Reopen'}
          </button>
        ) : (
          <button type="button" className="settings__danger" disabled={busy} onClick={() => apply(true)}>
            {busy ? 'Closing...' : 'Close temporarily'}
          </button>
        )}
      </div>
    </section>
  );
}

function ScheduleCard({ schedule, onScheduleChange, onRefreshStatus }) {
  const days = groupByDay(schedule);

  function afterChange(next) {
    onScheduleChange(next);
    onRefreshStatus();
  }

  return (
    <section className="settings__card">
      <h2 className="settings__card-title">Opening hours</h2>
      <p className="settings__hint">
        A closing time earlier than the opening time means the shop closes after midnight. A day
        with no rows is closed. Windows cannot overlap, including across midnight.
      </p>

      <ul className="schedule">
        {days.map(({ day, windows }) => (
          <li key={day} className="schedule__day">
            <h3 className="schedule__day-name">{day}</h3>

            <div className="schedule__windows">
              {windows.length === 0 && <p className="schedule__closed">Closed</p>}

              {windows.map((window) => (
                <WindowRow
                  key={window.id}
                  window={window}
                  onSaved={(updated) =>
                    afterChange(schedule.map((w) => (w.id === updated.id ? updated : w)))
                  }
                  onDeleted={() => afterChange(schedule.filter((w) => w.id !== window.id))}
                />
              ))}

              <NewWindowRow
                day={day}
                onAdded={(created) => afterChange([...schedule, created])}
              />
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}

function WindowRow({ window, onSaved, onDeleted }) {
  const [openTime, setOpenTime] = useState(toTimeInput(window.openTime));
  const [closeTime, setCloseTime] = useState(toTimeInput(window.closeTime));
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  const dirty =
    openTime !== toTimeInput(window.openTime) || closeTime !== toTimeInput(window.closeTime);

  async function handleSave() {
    const payload = {
      dayOfWeek: window.dayOfWeek,
      openTime: fromTimeInput(openTime),
      closeTime: fromTimeInput(closeTime),
    };
    setBusy(true);
    setError(null);
    try {
      await updateWindow(window.id, payload);
      // 204, so the row is rebuilt locally from what was sent.
      onSaved({ ...window, ...payload });
    } catch (err) {
      // 400 carries the reason (overlap, or zero-length), which is worth showing verbatim.
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete() {
    if (!confirming) return setConfirming(true);
    setConfirming(false);
    setBusy(true);
    setError(null);
    try {
      await deleteWindow(window.id);
      onDeleted();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="window-row">
      <div className="window-row__fields">
        <input
          className="settings__input window-row__time"
          type="time"
          value={openTime}
          onChange={(e) => setOpenTime(e.target.value)}
          aria-label={`Opening time on ${window.dayOfWeek}`}
          disabled={busy}
        />
        <span className="window-row__to">to</span>
        <input
          className="settings__input window-row__time"
          type="time"
          value={closeTime}
          onChange={(e) => setCloseTime(e.target.value)}
          aria-label={`Closing time on ${window.dayOfWeek}`}
          disabled={busy}
        />

        <span className="window-row__desc">{describeWindow(window.openTime, window.closeTime)}</span>

        <button type="button" className="settings__save" disabled={!dirty || busy} onClick={handleSave}>
          Save
        </button>
        <button
          type="button"
          className={`settings__delete ${confirming ? 'settings__delete--confirming' : ''}`}
          disabled={busy}
          onClick={handleDelete}
        >
          {confirming ? 'Confirm' : 'Delete'}
        </button>
      </div>

      {error && (
        <p className="window-row__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

function NewWindowRow({ day, onAdded }) {
  const [openTime, setOpenTime] = useState('');
  const [closeTime, setCloseTime] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function handleAdd(event) {
    event.preventDefault();
    if (!openTime || !closeTime) return setError('Set both times.');
    setBusy(true);
    setError(null);
    try {
      const created = await createWindow({
        dayOfWeek: day,
        openTime: fromTimeInput(openTime),
        closeTime: fromTimeInput(closeTime),
      });
      onAdded(created);
      setOpenTime('');
      setCloseTime('');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="window-new" onSubmit={handleAdd}>
      <input
        className="settings__input window-row__time"
        type="time"
        value={openTime}
        onChange={(e) => setOpenTime(e.target.value)}
        aria-label={`New opening time on ${day}`}
        disabled={busy}
      />
      <span className="window-row__to">to</span>
      <input
        className="settings__input window-row__time"
        type="time"
        value={closeTime}
        onChange={(e) => setCloseTime(e.target.value)}
        aria-label={`New closing time on ${day}`}
        disabled={busy}
      />
      <button type="submit" className="settings__add" disabled={busy}>
        {busy ? 'Adding...' : 'Add'}
      </button>
      {error && (
        <p className="window-row__error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}
