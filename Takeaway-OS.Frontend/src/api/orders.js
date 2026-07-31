import { api } from './client.js';

// Mirrors the OrderType enum in the API. 
// Program.cs registers a JsonStringEnumConverter, so enums cross the wire as their NAME ("Collection"), 
// never as the underlying number.
export const ORDER_TYPES = {
  Collection: 'Collection',
  Delivery: 'Delivery',
};

// Mirrors the OrderStatus enum, same string-name-over-the-wire rule as ORDER_TYPES above.
// Listed in the order an order actually moves through them
export const ORDER_STATUSES = {
  Pending: 'Pending',
  Paid: 'Paid',
  Preparing: 'Preparing',
  Ready: 'Ready',
  OutForDelivery: 'OutForDelivery',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

// Basket line -> OrderItemCreateDto. Lives here, not in the page, because the shape it
// produces is part of the API contract, and that is what this layer owns.
//
// Deliberately narrow: the basket line also carries name, unitPrice and the derived key,
// and NONE of them are sent. The server re-reads name and price from MenuItems and
// snapshots them itself, so a tampered price in localStorage can't change what is charged.
function toOrderItemDto(line) {
  return {
    menuItemId: line.menuItemId,
    quantity: line.quantity,
    notes: line.notes,
    modifierOptionIds: line.modifiers.map((m) => m.id), // server re-looks-up each option's name + priceDelta
  };
}

// POST /api/orders
// Body: OrderCreateDto { customerName, customerPhone, deliveryAddress, addressId, orderType, notes, items[] }
// Returns: OrderCreateResponseDto { order, clientSecret }
//
// auth: true (the default) on purpose even though the endpoint is [AllowAnonymous]:
// a guest simply has no token to send, while a logged-in customer's token is what lets
// the server stamp CustomerId onto the order instead of leaving it null.
//
// No total is sent. The server computes it from current menu prices — sending one would
// be asking the client to name its own price.
export function createOrder({ customerName, customerPhone, deliveryAddress, addressId, orderType, notes, lines }) {
  return api.post('/api/orders', {
    customerName,
    customerPhone,
    deliveryAddress,
    addressId,
    orderType,
    notes,
    items: lines.map(toOrderItemDto),
  });
}

// GET /api/orders/by-token/{token}
// Returns: GuestOrderDto { id, orderType, status, deliveryAddress, notes, createdAt, items[], total }
export function getOrderByToken(token) {
  // A GUID has no characters that need escaping, but the encode stays: this value reaches us from a
  // URL segment, and building a request path out of unescaped input is the habit worth not forming.
  return api.get(`/api/orders/by-token/${encodeURIComponent(token)}`, { auth: false });
}

// --- Owner-only ---

// GET /api/orders
// Returns: OrderDto[] - every order ever placed, newest first. No server-side filter or paging,
// so the dashboard trims client-side. First thing to revisit if the list gets slow.
export function getAllOrders() {
  return api.get('/api/orders');
}

// PUT /api/orders/{id}/status -> 204
export function updateOrderStatus(id, status) {
  return api.put(`/api/orders/${id}/status`, { status });
}

// PUT /api/orders/{id}/driver -> 204. driverId null unassigns, which keeps it idempotent.
export function assignDriver(id, driverId) {
  return api.put(`/api/orders/${id}/driver`, { driverId });
}

// --- Driver-only ---

// GET /api/orders/assigned
// Returns: OrderDto[] scoped to the caller. No driver id to pass - the server reads it off the JWT.
export function getAssignedOrders() {
  return api.get('/api/orders/assigned');
}

// PUT /api/orders/{id}/delivery-status -> 204
// Separate from updateOrderStatus: the server allows only Ready -> OutForDelivery -> Completed here.
export function updateDeliveryStatus(id, status) {
  return api.put(`/api/orders/${id}/delivery-status`, { status });
}
