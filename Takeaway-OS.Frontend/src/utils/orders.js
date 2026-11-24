import { ORDER_STATUSES, ORDER_TYPES } from '../api/orders.js';

// Human labels for the enum names that aren't already readable.
const STATUS_LABELS = {
  [ORDER_STATUSES.OutForDelivery]: 'Out for delivery',
};

export function formatStatus(status) {
  return STATUS_LABELS[status] ?? status;
}

// Mirrors OrderService.IsValidTransition. The server re-checks every one of these and 400s on a
// bad pair, so this exists to decide which BUTTON to draw, never to authorise anything.
// Returns [] for Pending (hidden from the dashboard), Completed and Cancelled (terminal).
export function ownerActions(order) {
  switch (order.status) {
    case ORDER_STATUSES.Paid:
      return [
        { status: ORDER_STATUSES.Preparing, label: 'Start preparing', variant: 'primary' },
        // Says "refund" because that's what the server now does: cancelling a Paid order refunds
        // the customer through Stripe in the same action, so the button should not hide it.
        { status: ORDER_STATUSES.Cancelled, label: 'Cancel & refund', variant: 'danger' },
      ];
    case ORDER_STATUSES.Preparing:
      return [{ status: ORDER_STATUSES.Ready, label: 'Mark ready', variant: 'primary' }];
    case ORDER_STATUSES.Ready:
      // The one status whose next step depends on the order type, not just the status.
      return order.orderType === ORDER_TYPES.Delivery
        ? [{ status: ORDER_STATUSES.OutForDelivery, label: 'Out for delivery', variant: 'primary' }]
        : [{ status: ORDER_STATUSES.Completed, label: 'Complete', variant: 'primary' }];
    case ORDER_STATUSES.OutForDelivery:
      return [{ status: ORDER_STATUSES.Completed, label: 'Complete', variant: 'primary' }];
    default:
      return [];
  }
}

// Mirrors IsValidDriverTransition, which is a whitelist of exactly two pairs and NOT a subset
// check against the owner's rules. A driver never gets a cancel action.
export function driverAction(order) {
  switch (order.status) {
    case ORDER_STATUSES.Ready:
      return { status: ORDER_STATUSES.OutForDelivery, label: 'Start delivery' };
    case ORDER_STATUSES.OutForDelivery:
      return { status: ORDER_STATUSES.Completed, label: 'Mark delivered' };
    default:
      return null;
  }
}

// The owner dashboard's tabs, in flow order. `statuses` is what each tab filters to; History
// deliberately merges the two terminal statuses rather than giving each its own tab, and only ever
// holds the last 24 hours, because that's all GET /api/orders/board returns.
export const OWNER_TABS = [
  { key: 'paid', label: 'Paid', statuses: [ORDER_STATUSES.Paid] },
  { key: 'preparing', label: 'Preparing', statuses: [ORDER_STATUSES.Preparing] },
  { key: 'ready', label: 'Ready', statuses: [ORDER_STATUSES.Ready] },
  { key: 'out', label: 'Out', statuses: [ORDER_STATUSES.OutForDelivery] },
  {
    key: 'history',
    label: 'History',
    statuses: [ORDER_STATUSES.Completed, ORDER_STATUSES.Cancelled],
  },
];

// Minutes since a timestamp from the API. Both paidAt and createdAt are UTC with a trailing Z, so
// Date parses them correctly without any manual offset.
//
// OrderCard passes paidAt (when payment was confirmed), falling back to createdAt for orders placed
// before PaidAt was recorded, which would otherwise read high by however long the customer spent
// on the Stripe form.
export function minutesSince(timestamp) {
  return Math.max(0, Math.floor((Date.now() - new Date(timestamp).getTime()) / 60000));
}

export function formatWaiting(timestamp) {
  const mins = minutesSince(timestamp);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins} min`;
  const hours = Math.floor(mins / 60);
  return `${hours}h ${mins % 60}m`;
}
