// Monday first, which is how a UK week reads on a shop door. Deliberately NOT the enum's own
// order: C#'s DayOfWeek starts at Sunday = 0, and the API sends the NAME, so nothing here
// depends on the numeric value.
export const DAYS = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
];

// TimeOnly serialises as "17:00:00" (sometimes with fractional seconds), while <input type="time">
// reads and writes "17:00". Truncating to minutes is safe for real opening hours, but it is a
// one-way trip: a window stored as 23:59:59.999999 comes back as 23:59:00 once saved through
// this UI. Only a hand-written 24/7 row looks like that, and it is not a shape the owner can
// produce here anyway.
export function toTimeInput(apiTime) {
  return apiTime.slice(0, 5);
}

export function fromTimeInput(inputTime) {
  return `${inputTime}:00`;
}

// CloseTime <= OpenTime is how the schema encodes "closes after midnight" — there is no end-date
// column. The API's own validator rejects only the EQUAL case, as ambiguous between a zero-length
// window and a full 24 hours.
export function isPastMidnight(openTime, closeTime) {
  return closeTime <= openTime;
}

export function describeWindow(openTime, closeTime) {
  const open = toTimeInput(openTime);
  const close = toTimeInput(closeTime);
  return isPastMidnight(openTime, closeTime) ? `${open} to ${close} next day` : `${open} to ${close}`;
}

// One bucket per day, including the empty ones: a day with no windows is closed, and the owner
// needs to see that row to add the first window to it.
export function groupByDay(schedule) {
  return DAYS.map((day) => ({
    day,
    windows: schedule
      .filter((w) => w.dayOfWeek === day)
      .sort((a, b) => a.openTime.localeCompare(b.openTime)),
  }));
}
