import { useCallback, useEffect, useState } from 'react';
import { getAssignedOrders, updateDeliveryStatus, ORDER_STATUSES } from '../api/orders.js';
import { driverAction } from '../utils/orders.js';
import OrderCard from '../components/OrderCard.jsx';
import './DriverDashboardPage.css';

const POLL_MS = 15000; // slower than the owner's 10s: a driver is on mobile data, not wifi

// Orders the driver has finished. Kept off the list rather than shown greyed, because unlike a
// not-yet-ready order there is nothing still coming.
const DONE = [ORDER_STATUSES.Completed, ORDER_STATUSES.Cancelled];

export default function DriverDashboardPage() {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busyIds, setBusyIds] = useState(() => new Set());

  const load = useCallback(async () => {
    try {
      setOrders(await getAssignedOrders());
      setError(null);
    } catch (err) {
      setError(err.message);
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      await load();
      if (active) setLoading(false);
    })();
    return () => {
      active = false;
    };
  }, [load]);

  useEffect(() => {
    const onVisibility = () => {
      if (document.visibilityState === 'visible') load();
    };
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') load();
    }, POLL_MS);

    document.addEventListener('visibilitychange', onVisibility);
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, [load]);

  function setBusy(id, isBusy) {
    setBusyIds((current) => {
      const next = new Set(current);
      if (isBusy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  async function handleAction(order, nextStatus) {
    const previousStatus = order.status;
    setOrders((current) =>
      current.map((o) => (o.id === order.id ? { ...o, status: nextStatus } : o)),
    );
    setBusy(order.id, true);
    try {
      await updateDeliveryStatus(order.id, nextStatus);
    } catch (err) {
      setOrders((current) =>
        current.map((o) => (o.id === order.id ? { ...o, status: previousStatus } : o)),
      );
      setError(err.message);
    } finally {
      setBusy(order.id, false);
    }
  }

  // Oldest first: the delivery waiting longest goes out next.
  const active = orders
    .filter((o) => !DONE.includes(o.status))
    .sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt));

  if (loading) return <p className="driver__message">Loading your deliveries...</p>;

  return (
    <div className="driver">
      <h1 className="driver__title">My deliveries</h1>

      {error && (
        <p className="driver__error" role="alert">
          {error}
          <button type="button" className="driver__retry" onClick={load}>
            Retry
          </button>
        </p>
      )}

      {active.length === 0 ? (
        <p className="driver__message">Nothing assigned to you right now.</p>
      ) : (
        <ul className="driver__list">
          {active.map((order) => {
            const action = driverAction(order);
            return (
              <OrderCard
                key={order.id}
                order={order}
                variant="driver"
                actions={action ? [{ ...action, variant: 'primary' }] : []}
                onAction={(status) => handleAction(order, status)}
                // No action means the kitchen hasn't finished it: informational, not actionable.
                muted={action === null}
                busy={busyIds.has(order.id)}
              />
            );
          })}
        </ul>
      )}
    </div>
  );
}
