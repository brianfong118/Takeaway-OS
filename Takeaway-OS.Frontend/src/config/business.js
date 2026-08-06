// The real-world identity of the business behind this site.
//
// Consumer Contracts Regulations require the trading name, a geographic address and contact
// details to be visible BEFORE a customer places an order, and UK GDPR requires the same details
// again on the privacy policy as the data controller. Those are the same facts in two places,
// so they live here once: a footer and a privacy policy that disagree about the address is worse
// than either being missing, and that is exactly what two hand-typed copies drift into.
//
// Plain constants rather than an owner-editable setting on purpose. These change when the
// business moves or renames, which is a redeploy-sized event, not a Tuesday-afternoon one, and
// making them editable would let a typo in an admin form quietly break a legal requirement.

export const BUSINESS = {
  tradingName: 'Amethyst',
  addressLines: ['734 Upper Newtownards Road', 'Belfast', 'Northern Ireland'],
  postcode: 'BT4 3HE',
  phone: '028 9048 1188',
  email: 'brianfong118@gmail.com',

  // Set these three only if the business is a registered limited company; leave them null if it
  // is a sole trader or partnership, and the footer omits the line entirely rather than printing
  // "null". registeredIn is the jurisdiction the company was incorporated in, which for a
  // Belfast business is Northern Ireland, NOT "England and Wales" - Companies House keeps a
  // separate NI register, and that phrase is the single most-copied wrong detail in UK footers.
  companyNumber: null,
  registeredName: null,
  registeredIn: 'Northern Ireland',
};

export const PRIVACY_POLICY_UPDATED = '6 August 2026';

export function formatAddressInline() {
  return [...BUSINESS.addressLines, BUSINESS.postcode].join(', ');
}
