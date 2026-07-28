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

// Modifier deltas apply per unit, so they are added before multiplying by quantity.
// Display only. OrderService.ComputeTotal produces the amount actually charged.
export function lineTotal(line) {
  const perUnit = line.unitPrice + line.modifiers.reduce((sum, m) => sum + m.priceDelta, 0);
  return perUnit * line.quantity;
}
