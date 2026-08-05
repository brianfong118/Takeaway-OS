import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import AccountNav from '../components/AccountNav.jsx';
import { ORDER_STATUSES, ORDER_TYPES, getMyOrders } from '../api/orders.js';
import { formatStatus } from '../utils/orders.js';
import { formatPrice } from '../utils/format.js';
import './AccountPage.css';
import './OrderHistoryPage.css';

// Placed but never paid for. An abandoned checkout and a failed payment look identical from
// here, and neither is an order the kitchen will ever see ,showing them would list things
// the customer cannot act on and did not get. Same exclusion the owner's dashboard makes.
function isVisible(order) {
  return order.status !== ORDER_STATUSES.Pending;
}

// en-GB so it reads 4 Aug, not Aug 4.
const dateFormat = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

// createdAt is UTC with a trailing Z, so Date parses it correctly and Intl renders it in the
// reader's OWN time zone - which is the right one here, unlike the business-hours check that
// has to use the restaurant's.
function formatPlaced(createdAt) {
  return dateFormat.format(new Date(createdAt));
}

export default function OrderHistoryPage() {
  const [orders, setOrders] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let ignore = false;

    (async () => {
      try {
        const loaded = await getMyOrders();
        if (!ignore) setOrders(loaded);
      } catch (err) {
        if (!ignore) setError(err);
      } finally {
        if (!ignore) setIsLoading(false);
      }
    })();

    return () => {
      ignore = true;
    };
  }, []);

  const visible = orders.filter(isVisible);

  return (
    <div className="account">
      <h1 className="account__title">Your account</h1>
      <AccountNav />

      {isLoading && <p className="account__status">Loading your orders...</p>}
      {error && <p className="account__error" role="alert">{error.message}</p>}

      {!isLoading && !error && visible.length === 0 && (
        <p className="history__empty">
          You haven’t placed any orders yet. <Link to="/">Browse the menu</Link> to get started.
        </p>
      )}

      <ul className="history__list">
        {visible.map((order) => (
          <OrderHistoryCard key={order.id} order={order} />
        ))}
      </ul>

      {!isLoading && !error && (
        <p className="history__note">
          Orders you placed as a guest aren’t listed here — they’re reachable only from the
          confirmation link you were given at the time.
        </p>
      )}
    </div>
  );
}

function OrderHistoryCard({ order }) {
  const isDelivery = order.orderType === ORDER_TYPES.Delivery;

  const statusClass =
    order.status === ORDER_STATUSES.Cancelled
      ? 'history__status history__status--cancelled'
      : order.status === ORDER_STATUSES.Completed
        ? 'history__status history__status--done'
        : 'history__status history__status--active';

  return (
    <li className="history__item">
      <div className="history__head">
        <div>
          <p className="history__id">Order #{order.id}</p>
          <p className="history__placed">{formatPlaced(order.createdAt)}</p>
        </div>
        <span className={statusClass}>{formatStatus(order.status)}</span>
      </div>

      <ul className="history__lines">
        {order.items.map((item) => (
          <li key={item.id} className="history__line">
            <span>
              {item.quantity} &times; {item.itemName}
              {item.modifiers.length > 0 && (
                <span className="history__mods">
                  {item.modifiers.map((m) => m.name).join(', ')}
                </span>
              )}
            </span>
          </li>
        ))}
      </ul>

      <div className="history__foot">
        <span className="history__type">
          {isDelivery ? order.deliveryAddress : 'Collection'}
        </span>
        <span className="history__total">{formatPrice(order.total)}</span>
      </div>

      {order.deliveryFee > 0 && (
        <p className="history__fee">Includes {formatPrice(order.deliveryFee)} delivery.</p>
      )}
    </li>
  );
}
