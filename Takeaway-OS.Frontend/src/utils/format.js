// Built once at module load, not per call , constructing a formatter is the expensive part.
const gbp = new Intl.NumberFormat('en-GB', {
  style: 'currency',
  currency: 'GBP',
});

// 8.5 -> "£8.50". C# decimal serialises to a JSON number, so trailing zeros are already gone.
export function formatPrice(amount) {
  return gbp.format(amount);
}

// Modifier deltas need an explicit sign: "+£0.50", "-£1.00", and "" when free.
export function formatPriceDelta(amount) {
  if (amount === 0) return '';
  return amount > 0 ? `+${gbp.format(amount)}` : `-${gbp.format(Math.abs(amount))}`;
}
