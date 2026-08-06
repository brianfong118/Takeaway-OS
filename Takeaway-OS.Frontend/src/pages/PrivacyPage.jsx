import { BUSINESS, PRIVACY_POLICY_UPDATED, formatAddressInline } from '../config/business.js';
import './PrivacyPage.css';

// Static content, so no state, no effects and no API call 
//
// The identity details come from config/business.js rather than being typed into the prose,
// because this page names the same business as the footer does. A privacy policy that gives a
// different address from the footer is a policy that cannot be relied on.
//
// Written to cover what UK GDPR requires a policy to say: what is collected, why, on what legal
// basis, how long it is kept, who else processes it, and how to exercise your rights over it.
export default function PrivacyPage() {
  return (
    <div className="privacy">
      <h1>Privacy policy</h1>
      <p className="privacy__updated">Last updated: {PRIVACY_POLICY_UPDATED}</p>

      <section className="privacy__section">
        <h2>Who we are</h2>
        <p>
          {BUSINESS.tradingName} ({formatAddressInline()}) is the data controller for the personal
          information described below. That means we decide what is collected and why, and we are
          responsible for looking after it.
        </p>
        <p>
          You can reach us on <a href={`tel:${BUSINESS.phone.replace(/\s/g, '')}`}>{BUSINESS.phone}</a>{' '}
          or at <a href={`mailto:${BUSINESS.email}`}>{BUSINESS.email}</a>.
        </p>
      </section>

      <section className="privacy__section">
        <h2>What we collect, and why</h2>

        <h3>When you place an order</h3>
        <p>
          We collect your name, phone number, and - for delivery orders - your delivery address and
          postcode, along with the items you ordered and any notes you add for the kitchen. We need
          these to accept payment, cook the right food and get it to the right door, and to phone
          you if something about your order is unclear. The legal basis is performance of a
          contract: this is the information required to fulfil the order you asked for.
        </p>
        <p>
          You do not need an account to order. If you order as a guest, we hold the details above
          for that order and nothing more.
        </p>

        <h3>If you create an account</h3>
        <p>
          We additionally store your email address, a securely hashed version of your password (we
          never store the password itself), and any delivery addresses you choose to save. Your
          past orders are linked to the account so you can see them again. The legal basis is again
          performance of a contract - the account and the features it provides are the service you
          signed up for.
        </p>

        <h3>Payment details</h3>
        <p>
          <strong>We never see or store your card number.</strong> Card details are entered directly
          into Stripe, our payment provider, and are handled entirely by them. All we keep is
          Stripe's reference for the payment, so we can match a payment to an order and issue a
          refund if one is due.
        </p>

        <h3>Information stored on your device</h3>
        <p>
          Your basket is saved in your browser's local storage so it survives a page refresh, and if
          you log in, your sign-in token is stored the same way so you are not asked to log in on
          every page. Both stay on your device, are not sent anywhere except to us as part of using
          the site, and are cleared when you empty your basket or log out. We do not use advertising
          or tracking cookies, and we do not profile you or make automated decisions about you.
        </p>
      </section>

      <section className="privacy__section">
        <h2>Who else handles your information</h2>
        <p>
          We use a small number of service providers to run the site. They process information on
          our instructions and are not permitted to use it for their own purposes:
        </p>
        <ul>
          <li>
            <strong>Stripe</strong> - takes and processes card payments.
          </li>
          <li>
            <strong>Render</strong> - hosts our ordering system and its database, where your order
            and account details are stored.
          </li>
          <li>
            <strong>Vercel</strong> - hosts this website itself.
          </li>
        </ul>
        <p>
          We do not sell your information, and we do not share it with anyone for marketing. We will
          only disclose it otherwise where the law requires it.
        </p>
      </section>

      <section className="privacy__section">
        <h2>How long we keep it</h2>
        <p>
          Order records, including the name, phone number and address attached to an order, are kept
          for six years. This is not a choice we make: records of sales are business records, and
          HMRC requires them to be retained for six years after the end of the accounting period
          they fall in. The legal basis for keeping them that long is our legal obligation.
        </p>
        <p>
          Account details and saved addresses are kept for as long as you have an account. If you
          ask us to delete your account we will do so, but note that the order records above must
          still be retained for the six-year period - what we can remove is your account, your saved
          addresses and the link between you and those past orders.
        </p>
      </section>

      <section className="privacy__section">
        <h2>Your rights</h2>
        <p>You have the right to:</p>
        <ul>
          <li>ask for a copy of the personal information we hold about you;</li>
          <li>have inaccurate information corrected;</li>
          <li>have your information deleted, subject to the retention period above;</li>
          <li>object to or ask us to restrict how we use it;</li>
          <li>receive the information you gave us in a portable format.</li>
        </ul>
        <p>
          To do any of these, email <a href={`mailto:${BUSINESS.email}`}>{BUSINESS.email}</a> or
          phone us on{' '}
          <a href={`tel:${BUSINESS.phone.replace(/\s/g, '')}`}>{BUSINESS.phone}</a>. We will respond
          within one month. There is no charge.
        </p>
        <p>
          If you are unhappy with how we have handled your information, you can complain to the
          Information Commissioner's Office at{' '}
          <a href="https://ico.org.uk" target="_blank" rel="noreferrer">
            ico.org.uk
          </a>
          , or on 0303 123 1113. We would rather you came to us first so we can put it right.
        </p>
      </section>

      <section className="privacy__section">
        <h2>Changes to this policy</h2>
        <p>
          If we change how we handle personal information we will update this page and the date at
          the top of it.
        </p>
      </section>
    </div>
  );
}
