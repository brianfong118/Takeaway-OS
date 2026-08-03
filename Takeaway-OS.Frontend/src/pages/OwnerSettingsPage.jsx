import { useEffect, useState } from 'react';
import {
  getSchedule,
  getRestaurantStatus,
  createWindow,
  updateWindow,
  deleteWindow,
  setClosure,
} from '../api/openingHours.js';
import { getSettings, updateSettings } from '../api/settings.js';
import { getDeliveryAreas, createDeliveryArea, deleteDeliveryArea } from '../api/deliveryAreas.js';
import { groupByDay, toTimeInput, fromTimeInput, describeWindow } from '../utils/hours.js';
import OwnerNav from '../components/OwnerNav.jsx';
import './OwnerSettingsPage.css';

export default function OwnerSettingsPage() {
  const [schedule, setSchedule] = useState([]);
  const [status, setStatus] = useState(null);
  const [deliveryFee, setDeliveryFee] = useState(null);
  const [areas, setAreas] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [windows, currentStatus, settings, deliveryAreas] = await Promise.all([
          getSchedule(),
          getRestaurantStatus(),
          getSettings(),
          getDeliveryAreas(),
        ]);
        if (!active) return;
        setSchedule(windows);
        setStatus(currentStatus);
        setDeliveryFee(settings.deliveryFee);
        setAreas(deliveryAreas);
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

      {/* Guarded because the card seeds its input from this number, and null would render an
          uncontrolled input that React then complains about the moment it gets a value. */}
      {deliveryFee !== null && (
        <DeliveryFeeCard fee={deliveryFee} onFeeChange={setDeliveryFee} />
      )}

      {/* No null guard needed, unlike the fee card: this one seeds its input from an empty
          string, not from the fetched data, so an empty list is a valid thing to render. */}
      <DeliveryAreaCard areas={areas} onAreasChange={setAreas} />
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

function DeliveryFeeCard({ fee, onFeeChange }) {
  // A STRING, not a number, for the reason MenuItemForm's price field is: an <input> hands back
  // "" while the box is empty, and Number('') is 0, so numeric state would turn a half-cleared
  // box into free delivery. toFixed(2) because 2.5 from JSON should read as "2.50" in a price box.
  const [value, setValue] = useState(fee.toFixed(2));
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState(null);

  const parsed = Number(value);
  const isValid = value.trim() !== '' && Number.isFinite(parsed) && parsed >= 0 && parsed <= 100;
  const dirty = isValid && parsed !== fee;

  async function handleSave(event) {
    event.preventDefault();
    if (!dirty) return;

    setBusy(true);
    setError(null);
    try {
      // Returns the saved settings, so the box is re-seeded from the server's value rather than
      // from what was typed. "2.5" typed in comes back as "2.50".
      const next = await updateSettings({ deliveryFee: parsed });
      onFeeChange(next.deliveryFee);
      setValue(next.deliveryFee.toFixed(2));
      setSaved(true);
    } catch (err) {
      // 400 carries the Range message from RestaurantSettingsUpdateDto, worth showing verbatim.
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="settings__card">
      <h2 className="settings__card-title">Delivery fee</h2>
      <p className="settings__hint">
        Charged once per order, on delivery orders only. Changing it affects new orders only -
        orders already placed keep the fee they were charged.
      </p>

      {error && (
        <p className="settings__error" role="alert">
          {error}
        </p>
      )}

      <form className="fee__controls" onSubmit={handleSave}>
        <label className="fee__field">
          <span className="settings__label">Fee</span>
          <div className="fee__input-wrap">
            <span className="fee__prefix" aria-hidden="true">
              &pound;
            </span>
            <input
              className="settings__input fee__input"
              // type="number" gives a numeric keypad on mobile and the browser's own stepper.
              // It does NOT stop a bad value reaching us, hence isValid above and Range on the DTO.
              type="number"
              inputMode="decimal"
              min="0"
              max="100"
              step="0.01"
              value={value}
              onChange={(e) => {
                setValue(e.target.value);
                setSaved(false); // a fresh edit must not sit under a stale "Saved"
              }}
              disabled={busy}
              aria-label="Delivery fee in pounds"
            />
          </div>
        </label>

        <button type="submit" className="settings__primary" disabled={!dirty || busy}>
          {busy ? 'Saving...' : 'Save'}
        </button>

        {/* The only feedback available: nothing else on this page changes when the fee is saved. */}
        {saved && !dirty && <span className="fee__saved">Saved</span>}
      </form>
    </section>
  );
}

function DeliveryAreaCard({ areas, onAreasChange }) {
  const [value, setValue] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function handleAdd(event) {
    event.preventDefault();

    const outwardCode = value.trim();
    if (!outwardCode) return setError('Enter a postcode district.');

    setBusy(true);
    setError(null);
    try {
      // 201 returns the created row, and the server has NORMALISED it 
      const created = await createDeliveryArea({ outwardCode });

      // Re-sorted locally to match the server's alphabetical GET, rather than refetching the
      // whole list for one row. Safe to .sort() (which mutates, unlike LINQ OrderBy) because
      // the spread has already produced a brand-new array
      onAreasChange([...areas, created].sort((a, b) => a.outwardCode.localeCompare(b.outwardCode)));
      setValue('');
    } catch (err) {
      // 400 (not a district) and 409 (already listed) both carry a usable sentence from the
      // controller, so neither needs rewording here.
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="settings__card">
      <h2 className="settings__card-title">Delivery area</h2>
      <p className="settings__hint">
        Enter the <strong>district</strong> only &mdash; the part before the space, like E1 or
        SW1A. A delivery order is refused unless its postcode is in one of these districts.
        Districts are exact: listing E1 does <strong>not</strong> include E14.
      </p>

      {areas.length === 0 && (
        <p className="areas__empty" role="alert">
          No districts listed, so <strong>every delivery order is currently refused</strong>. Add
          at least one district to start taking deliveries. Collection orders are unaffected.
        </p>
      )}

      {error && (
        <p className="settings__error" role="alert">
          {error}
        </p>
      )}

      {areas.length > 0 && (
        <ul className="areas__list">
          {areas.map((area) => (
            <AreaRow
              key={area.id}
              area={area}
              onDeleted={() => onAreasChange(areas.filter((a) => a.id !== area.id))}
            />
          ))}
        </ul>
      )}

      <form className="areas__new" onSubmit={handleAdd}>
        <label className="areas__field">
          <span className="settings__label">Add a district</span>
          <input
            className="settings__input areas__input"
            value={value}
            // Uppercased as it is typed, purely so the box matches what will be stored. The
            // server normalises regardless, so this is cosmetic, not the validation.
            onChange={(e) => {
              setValue(e.target.value.toUpperCase());
              setError(null);
            }}
            placeholder="E1"
            maxLength={8}
            disabled={busy}
          />
        </label>

        <button type="submit" className="settings__add" disabled={busy || value.trim() === ''}>
          {busy ? 'Adding...' : 'Add'}
        </button>
      </form>
    </section>
  );
}

function AreaRow({ area, onDeleted }) {
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  async function handleDelete() {
    // Two-step, matching WindowRow. Worth it here despite a district being trivial to re-add,
    // because deleting the last one silently stops the shop taking any delivery order at all.
    if (!confirming) return setConfirming(true);
    setConfirming(false);
    setBusy(true);
    setError(null);
    try {
      await deleteDeliveryArea(area.id);
      onDeleted();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="areas__row">
      <span className="areas__code">{area.outwardCode}</span>

      <button
        type="button"
        className={`settings__delete ${confirming ? 'settings__delete--confirming' : ''}`}
        disabled={busy}
        onClick={handleDelete}
        aria-label={`Remove ${area.outwardCode} from the delivery area`}
      >
        {confirming ? 'Confirm' : 'Remove'}
      </button>

      {error && (
        <p className="window-row__error" role="alert">
          {error}
        </p>
      )}
    </li>
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
