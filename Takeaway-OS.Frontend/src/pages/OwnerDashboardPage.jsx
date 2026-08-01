import { useCallback, useEffect, useRef, useState } from 'react';
import { getAllOrders, updateOrderStatus, assignDriver, ORDER_STATUSES } from '../api/orders.js';
import { getDrivers } from '../api/drivers.js';
import { OWNER_TABS, ownerActions, isDashboardVisible } from '../utils/orders.js';
import OrderCard from '../components/OrderCard.jsx';
import './OwnerDashboardPage.css';

const POLL_MS = 10000;

export default function OwnerDashboardPage() {
  const [orders, setOrders] = useState([]);
  const [drivers, setDrivers] = useState([]);
  const [activeTab, setActiveTab] = useState('preparing');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busyIds, setBusyIds] = useState(() => new Set());
  const [confirmingCancel, setConfirmingCancel] = useState(null);
  const [hasNewPaid, setHasNewPaid] = useState(false);

  // Read inside load(), which must not be rebuilt when the tab changes or the poll would restart.
  const activeTabRef = useRef(activeTab);
  useEffect(() => {
    activeTabRef.current = activeTab;
  }, [activeTab]);

  // null until the first load, so arriving to a backlog isn't reported as "new".
  const seenPaidIds = useRef(null);

  const load = useCallback(async () => {
    try {
      const data = await getAllOrders();
      const visible = data.filter(isDashboardVisible);
      setOrders(visible);
      setError(null);

      const paidIds = new Set(
        visible.filter((o) => o.status === ORDER_STATUSES.Paid).map((o) => o.id),
      );
      if (seenPaidIds.current === null) {
        seenPaidIds.current = paidIds;
        return;
      }
      // Compares ids, not the count: one order paid while another was started nets to zero.
      if (activeTabRef.current !== 'paid') {
        for (const id of paidIds) {
          if (!seenPaidIds.current.has(id)) {
            setHasNewPaid(true);
            break;
          }
        }
      }
      seenPaidIds.current = paidIds;
    } catch (err) {
      setError(err.message);
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      // Drivers failing must not blank the board - the dropdown is the only thing that needs them.
      await Promise.all([load(), getDrivers().then(setDrivers).catch(() => {})]);
      if (active) setLoading(false);
    })();
    return () => {
      active = false;
    };
  }, [load]);

  useEffect(() => {
    const onVisibility = () => {
      if (document.visibilityState === 'visible') load(); // catch up immediately, don't wait a full tick
    };
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') load(); // a backgrounded tablet polls nothing
    }, POLL_MS);

    document.addEventListener('visibilitychange', onVisibility);
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, [load]);

  function setBusy(id, isBusy) {
    setBusyIds((current) => {
      const next = new Set(current); // new Set, not a mutation - React compares by reference
      if (isBusy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  // Optimistic: the card moves on the tap, and the PUT reconciles afterwards. A kitchen tablet
  // can't afford a round trip of dead time per press.
  async function applyStatus(order, nextStatus) {
    const previousStatus = order.status;
    setOrders((current) =>
      current.map((o) => (o.id === order.id ? { ...o, status: nextStatus } : o)),
    );
    setBusy(order.id, true);
    try {
      await updateOrderStatus(order.id, nextStatus);
    } catch (err) {
      // Roll back only this order's status, not the whole list - a poll may have landed meanwhile.
      setOrders((current) =>
        current.map((o) => (o.id === order.id ? { ...o, status: previousStatus } : o)),
      );
      setError(err.message);
    } finally {
      setBusy(order.id, false);
    }
  }

  function handleAction(order, nextStatus) {
    // Cancelling is terminal, so it takes two taps. Inline rather than window.confirm, which
    // is easy to dismiss by reflex and blocks the page.
    if (nextStatus === ORDER_STATUSES.Cancelled && confirmingCancel !== order.id) {
      setConfirmingCancel(order.id);
      return;
    }
    setConfirmingCancel(null);
    applyStatus(order, nextStatus);
  }

  async function handleAssignDriver(order, driverId) {
    const previousDriverId = order.driverId;
    setOrders((current) =>
      current.map((o) => (o.id === order.id ? { ...o, driverId } : o)),
    );
    setBusy(order.id, true);
    try {
      await assignDriver(order.id, driverId);
    } catch (err) {
      setOrders((current) =>
        current.map((o) => (o.id === order.id ? { ...o, driverId: previousDriverId } : o)),
      );
      setError(err.message);
    } finally {
      setBusy(order.id, false);
    }
  }

  function selectTab(key) {
    setActiveTab(key);
    setConfirmingCancel(null); // a half-confirmed cancel must not survive a tab change
    if (key === 'paid') setHasNewPaid(false);
  }

  const tab = OWNER_TABS.find((t) => t.key === activeTab) ?? OWNER_TABS[0];

  // filter() already returned a fresh array, so sorting it in place can't mutate `orders`.
  // Oldest first on working tabs (cook what has waited longest), newest first on History.
  const visibleOrders = orders
    .filter((o) => tab.statuses.includes(o.status))
    .sort((a, b) =>
      tab.key === 'history'
        ? new Date(b.createdAt) - new Date(a.createdAt)
        : new Date(a.createdAt) - new Date(b.createdAt),
    );

  if (loading) return <p className="owner__message">Loading orders...</p>;

  return (
    <div className="owner">
      <h1 className="owner__title">Orders</h1>

      {/* Kept visible above the board rather than replacing it: a failed poll shouldn't hide
          the orders the kitchen is already working from. */}
      {error && (
        <p className="owner__error" role="alert">
          {error}
          <button type="button" className="owner__retry" onClick={load}>
            Retry
          </button>
        </p>
      )}

      <nav className="owner__tabs" aria-label="Order status">
        {OWNER_TABS.map((t) => {
          const count = orders.filter((o) => t.statuses.includes(o.status)).length;
          const alerting = t.key === 'paid' && hasNewPaid;
          return (
            <button
              key={t.key}
              type="button"
              // aria-current tells a screen reader which tab is selected; the class only paints it.
              aria-current={t.key === activeTab ? 'true' : undefined}
              className={[
                'owner__tab',
                t.key === activeTab ? 'owner__tab--active' : '',
                alerting ? 'owner__tab--alert' : '',
              ]
                .filter(Boolean)
                .join(' ')}
              onClick={() => selectTab(t.key)}
            >
              {t.label}
              {/* History is unbounded and its count means nothing at a glance. */}
              {t.key !== 'history' && <span className="owner__count">{count}</span>}
              {alerting && <span className="owner__new">New</span>}
            </button>
          );
        })}
      </nav>

      {visibleOrders.length === 0 ? (
        <p className="owner__message">Nothing here.</p>
      ) : (
        <ul className={`owner__board owner__board--${tab.key === 'preparing' ? 'full' : 'compact'}`}>
          {visibleOrders.map((order) => {
            const actions = ownerActions(order).map((action) =>
              action.status === ORDER_STATUSES.Cancelled && confirmingCancel === order.id
                ? { ...action, label: 'Confirm cancel' }
                : action,
            );
            return (
              <OrderCard
                key={order.id}
                order={order}
                variant={tab.key === 'preparing' ? 'full' : 'compact'}
                actions={actions}
                onAction={(status) => handleAction(order, status)}
                driverName={drivers.find((d) => d.id === order.driverId)?.name ?? null}
                // Preparing as well as Ready: assigning while the food cooks is what gives the
                // driver advance warning, and it's the only way an order is ever assigned but
                // not yet ready - the state the driver dashboard dims. Ready keeps it for late
                // changes.
                drivers={tab.key === 'ready' || tab.key === 'preparing' ? drivers : null}
                onAssignDriver={(driverId) => handleAssignDriver(order, driverId)}
                busy={busyIds.has(order.id)}
              />
            );
          })}
        </ul>
      )}
    </div>
  );
}
