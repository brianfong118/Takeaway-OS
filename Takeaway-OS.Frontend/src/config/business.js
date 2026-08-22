// The identity shown in the footer and named as the data controller in the privacy policy.
// Those are the same facts in two places, so they live here once: a footer and a privacy policy
// that disagree about who runs the site is worse than either being missing, and that is exactly
// what two hand-typed copies drift into.
//
// Plain constants rather than an owner-editable setting on purpose. These change when the
// business moves or renames, which is a redeploy-sized event, not a Tuesday-afternoon one, and
// making them editable would let a typo in an admin form quietly break a legal requirement.
//
// The block itself stays in the footer even though Consumer Contracts Regulations no longer
// bite here (nothing is being sold). Rendering it is the record that the requirement was known
// about; only the data behind it is now placeholder.

export const BUSINESS = {
  tradingName: 'TakeawayOS',
  addressLines: ['Belfast, Northern Ireland'],

  // null, not '', for both of these. The renderers test them with `&&` and skip the markup entirely 
  postcode: null,
  phone: null,

  // Kept: a portfolio demo that collects names and addresses still needs a reachable controller
  // for the data-rights requests the privacy policy promises to honour.
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

// Rendered under the footer address so the missing street/phone reads as deliberate rather than
// as fields somebody forgot to fill in.
export const DEMO_NOTICE = 'Demo deployment - portfolio project, no physical premises.';

export const PRIVACY_POLICY_UPDATED = '22 August 2026';

export function formatAddressInline() {
  // filter(Boolean) before the join, because postcode is now null: join() stringifies null to ''
  // and would leave a trailing ", " on the end of the address. Same guard the className lists use.
  return [...BUSINESS.addressLines, BUSINESS.postcode].filter(Boolean).join(', ');
}
