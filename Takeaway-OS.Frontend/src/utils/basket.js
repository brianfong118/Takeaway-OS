// Mirrors [Range(1, 5)] on OrderItemCreateDto.Quantity. Exceeding it is a 400 from the API.
export const MAX_LINE_QUANTITY = 5;

// Two lines are the same basket row only if they are the same item, same modifiers, same note
// Sorted so that picking A then B matches picking B then A.
// Default .sort() compares as strings ("10" < "9"), hence the numeric comparator.
export function lineKey({ menuItemId, modifiers, notes }) { 
  const ids = modifiers 
    .map((m) => m.id) 
    .sort((a, b) => a - b) 
    .join(',');
  return `${menuItemId}|${ids}|${notes.trim()}`;
}

// Mirrors OrderService.ComputeTotal exactly: (UnitPrice + sum(PriceDelta)) * Quantity.
// Modifiers are priced PER PORTION: "3 x Pizza, Extra cheese" is three pizzas that each get extra
// cheese, so the cheese is charged three times, same as the server.
// Also used by OrderConfirmationPage for order items, which carry the same
// unitPrice/quantity/modifiers[].priceDelta shape. One function is what stops the two drifting.
export function lineTotal(line) {
  const modifiers = line.modifiers.reduce((sum, m) => sum + m.priceDelta, 0);
  return (line.unitPrice + modifiers) * line.quantity;
}
