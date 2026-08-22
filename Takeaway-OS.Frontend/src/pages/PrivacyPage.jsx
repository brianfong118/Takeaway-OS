import { BUSINESS, PRIVACY_POLICY_UPDATED, formatAddressInline } from '../config/business.js';
import './PrivacyPage.css';

// Static content, so no state, no effects and no API call
//
// The identity details come from config/business.js rather than being typed into the prose,
// because this page names the same operator the footer does. A privacy policy that gives
// different details from the footer is a policy that cannot be relied on.
//
// Written to cover what UK GDPR requires a policy to say: what is collected, why, on what legal
// basis, how long it is kept, who else processes it, and how to exercise your rights over it.

export default function PrivacyPage() {
  return (
    <div className="privacy">
      <h1>Privacy policy</h1>
      <p className="privacy__updated">Last updated: {PRIVACY_POLICY_UPDATED}</p>

      <section className="privacy__section">
        <h2>About this site</h2>
        <p>
          {BUSINESS.tradingName} is a demonstration of a takeaway ordering system, built as a
          portfolio project for a family takeaway that has since closed. It is not a live
          restaurant: nothing on the menu can actually be bought, no food is prepared or delivered,
          and no payment made here is real - card payments run against Stripe&rsquo;s test mode,
          which is incapable of charging a real card.
        </p>
        <p>
          <strong>Please do not enter real personal details.</strong> The checkout asks for a name,
          phone number and address because a real ordering system would, and the form behaves
          exactly as it would in production. But nothing here needs to reach you, so invented
          details are the right thing to type. If you do enter real ones, everything below applies
          to them.
        </p>
      </section>

      <section className="privacy__section">
        <h2>Who we are</h2>
        <p>
          {BUSINESS.tradingName} is run by one individual, based in {formatAddressInline()}, as a
          personal portfolio project rather than as a business. That person is the data controller
          for the personal information described below: they decide what is collected and why, and
          are responsible for looking after it.
        </p>
        <p>
          You can reach them at <a href={`mailto:${BUSINESS.email}`}>{BUSINESS.email}</a>.
        </p>
      </section>

      <section className="privacy__section">
        <h2>What we collect, and why</h2>

        <h3>When you place a demo order</h3>
        <p>
          We collect the name, phone number and - for delivery orders - the delivery address and
          postcode you type in, along with the items you selected and any notes you added. These
          are stored because the system stores what a working ordering system would: the demo is
          only meaningful if the order that comes out the other end is a real record.
        </p>
        <p>
          The legal basis is your consent. Nothing obliges you to give these details, nothing is
          done with them beyond displaying them back to you and on the demo&rsquo;s own dashboards,
          and you can ask for them to be deleted at any time.
        </p>
        <p>
          You do not need an account to order. As a guest, the details above are held for that one
          order and nothing more.
        </p>

        <h3>If you create an account</h3>
        <p>
          We additionally store your email address, a securely hashed version of your password (we
          never store the password itself), and any delivery addresses you choose to save. Your
          past demo orders are linked to the account so you can see them again. The legal basis is
          again your consent - creating an account is entirely optional, and the whole site works
          without one.
        </p>

        <h3>Payment details</h3>
        <p>
          <strong>We never see or store your card number</strong>, and on this demo there is no
          real card number to see. Card details are entered directly into Stripe and handled
          entirely by them, and Stripe is running in test mode, so only Stripe&rsquo;s published
          test card numbers are accepted - a genuine card is rejected before any payment is
          attempted. All we keep is Stripe&rsquo;s reference for the test payment, so the demo can
          match a payment to an order.
        </p>

        <h3>Information stored on your device</h3>
        <p>
          Your basket is saved in your browser&rsquo;s local storage so it survives a page refresh,
          and if you log in, your sign-in token is stored the same way so you are not asked to log
          in on every page. Both stay on your device, are not sent anywhere except to us as part of
          using the site, and are cleared when you empty your basket or log out. We do not use
          advertising or tracking cookies, and we do not profile you or make automated decisions
          about you.
        </p>
      </section>

      <section className="privacy__section">
        <h2>Who else handles your information</h2>
        <p>
          A small number of service providers run the site. They process information on our
          instructions and are not permitted to use it for their own purposes:
        </p>
        <ul>
          <li>
            <strong>Stripe</strong> - processes the test card payments.
          </li>
          <li>
            <strong>Render</strong> - hosts the ordering system and its database, where demo order
            and account details are stored.
          </li>
          <li>
            <strong>Vercel</strong> - hosts this website itself.
          </li>
        </ul>
        <p>
          We do not sell your information, and we do not share it with anyone for marketing. We
          will only disclose it otherwise where the law requires it.
        </p>
      </section>

      <section className="privacy__section">
        <h2>How long we keep it</h2>
        <p>
          There is no retention period, because nothing here needs to be kept. The demo database is
          cleared periodically, and anything you have entered goes with it. A real takeaway would
          have to retain its order records for six years as business records, but there are no
          sales here and so no such obligation.
        </p>
        <p>
          If you would like something removed sooner than the next clear-out, ask and it will be
          deleted. Unlike a trading site, there is nothing we are required to hold back.
        </p>
      </section>

      <section className="privacy__section">
        <h2>Your rights</h2>
        <p>You have the right to:</p>
        <ul>
          <li>ask for a copy of the personal information we hold about you;</li>
          <li>have inaccurate information corrected;</li>
          <li>have your information deleted;</li>
          <li>object to or ask us to restrict how we use it;</li>
          <li>withdraw your consent at any time;</li>
          <li>receive the information you gave us in a portable format.</li>
        </ul>
        <p>
          To do any of these, email <a href={`mailto:${BUSINESS.email}`}>{BUSINESS.email}</a>. We
          will respond within one month. There is no charge.
        </p>
        <p>
          If you are unhappy with how we have handled your information, you can complain to the
          Information Commissioner&rsquo;s Office at{' '}
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
