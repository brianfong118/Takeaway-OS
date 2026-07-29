import { api } from './client.js';

// Mirrors the OrderType enum in the API. 
// Program.cs registers a JsonStringEnumConverter, so enums cross the wire as their NAME ("Collection"), 
// never as the underlying number.
export const ORDER_TYPES = {
  Collection: 'Collection',
  Delivery: 'Delivery',
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
