import { formatPrice, formatPriceDelta } from '../utils/format.js';
import { formatStatus, formatWaiting } from '../utils/orders.js';
import { ORDER_TYPES } from '../api/orders.js';
import './OrderCard.css';

export default function OrderCard({
  order,
  variant = 'compact',
  actions = [],
  onAction,
  driverName = null,
  drivers = null, // non-null = render the assignment dropdown
  onAssignDriver,
  busy = false,
}) {
  const isDelivery = order.orderType === ORDER_TYPES.Delivery;
  const itemCount = order.items.reduce((sum, item) => sum + item.quantity, 0);

  return (
    <li className={`order-card order-card--${variant}`}>
      <header className="order-card__header">
        <span className="order-card__number">#{order.id}</span>
        <span className={`order-card__type order-card__type--${isDelivery ? 'delivery' : 'collection'}`}>
          {isDelivery ? 'Delivery' : 'Collection'}
        </span>
        <span className="order-card__waiting">{formatWaiting(order.createdAt)}</span>
      </header>

      {variant === 'full' ? (
        <ul className="order-card__items">
          {order.items.map((item) => (
            <li key={item.id} className="order-card__item">
              <div className="order-card__item-line">
                <span className="order-card__qty">{item.quantity}x</span>
                <span className="order-card__item-name">{item.itemName}</span>
              </div>

              {item.modifiers.map((mod) => (
                <div key={mod.id} className="order-card__modifier">
                  {mod.name}
                  {/* Empty string for a zero delta, so "Medium" doesn't render "+£0.00". */}
                  <span className="order-card__delta">{formatPriceDelta(mod.priceDelta)}</span>
                </div>
              ))}

              {item.notes && <p className="order-card__item-notes">&ldquo;{item.notes}&rdquo;</p>}
            </li>
          ))}
        </ul>
      ) : (
        <p className="order-card__summary">
          {order.customerName} &middot; {itemCount} {itemCount === 1 ? 'item' : 'items'}
        </p>
      )}

      {order.notes && <p className="order-card__notes">&ldquo;{order.notes}&rdquo;</p>}

      {/* Address matters at handoff, not while cooking, so it stays off the full card. */}
      {variant === 'compact' && isDelivery && order.deliveryAddress && (
        <p className="order-card__address">{order.deliveryAddress}</p>
      )}

      {isDelivery && drivers && (
        <label className="order-card__driver">
          <span className="order-card__driver-label">Driver</span>
          <select
            className="order-card__driver-select"
            // A <select> value must be a string, and null would make it uncontrolled.
            value={order.driverId ?? ''}
            disabled={busy}
            onChange={(e) => onAssignDriver(e.target.value === '' ? null : Number(e.target.value))}
          >
            <option value="">Unassigned</option>
            {drivers.map((driver) => (
              <option key={driver.id} value={driver.id}>
                {driver.name}
                {driver.isAvailable ? '' : ' (unavailable)'}
              </option>
            ))}
          </select>
        </label>
      )}

      {isDelivery && !drivers && driverName && (
        <p className="order-card__driver-name">Driver: {driverName}</p>
      )}

      <footer className="order-card__footer">
        <span className="order-card__total">{formatPrice(order.total)}</span>

        {actions.length > 0 ? (
          <div className="order-card__actions">
            {actions.map((action) => (
              <button
                key={action.status}
                type="button"
                className={`order-card__button order-card__button--${action.variant}`}
                disabled={busy}
                onClick={() => onAction(action.status)}
              >
                {action.label}
              </button>
            ))}
          </div>
        ) : (
          <span className="order-card__status">{formatStatus(order.status)}</span>
        )}
      </footer>
    </li>
  );
}
