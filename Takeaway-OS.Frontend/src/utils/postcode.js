// Deliberately NOT a port of the API's UkPostcode class.
//
// The server owns two things this file refuses to duplicate: what a valid postcode looks like
// (a six-shape regex) and whether a district is deliverable (a database query).
//
// What IS mirrored is a single fact about the UK postcode format rather than a rule of ours:
// the inward code is always exactly three characters, so the outward code is everything before
//
// Everything here is advisory. The order is accepted or refused by OrderService, and the
// checkout never disables its submit button on the strength of this file's opinion.

// Uppercase, all whitespace removed -> matching how the server normalises before comparing.
export function normalisePostcode(raw) {
  return (raw ?? '').replace(/\s+/g, '').toUpperCase();
}

// The outward code, or '' when there aren't enough characters for one yet 
export function outwardCodeOf(raw) {
  const normalised = normalisePostcode(raw);

  // 5 is the shortest possible full postcode ("E16AN"): fewer characters than that and the
  // last three are not yet the inward code, they are just the start of one.
  if (normalised.length < 5) return '';

  return normalised.slice(0, -3);
}

// Whether the typed postcode looks like it falls outside the delivery area.
// Returns false (i.e. "no complaint") whenever we cannot tell: too few characters typed, or an
// area list that hasn't loaded
export function looksOutsideArea(raw, areas) {
  if (!areas || areas.length === 0) return false;

  const outward = outwardCodeOf(raw);
  if (outward === '') return false;

  // Exact equality, never startsWith 
  // .some() is JS's Any(): true as soon as one element matches, no LINQ needed.
  return !areas.some((area) => area.outwardCode === outward);
}
