import { NavLink } from 'react-router-dom';
import './OwnerNav.css';

// The owner's section switcher. It lives in the page rather than the top bar because the bar
// could not hold it: with Orders + Menu the staff bar measured 392px against a 375px phone, and
// a third link projected to ~477px. In here the bar stays at ~326px however many owner screens
// exist.
const SECTIONS = [
  // `end` on Orders only: "/owner" is a prefix of every other path here, so without it the
  // Orders tab would stay active on all of them.
  { to: '/owner', label: 'Orders', end: true },
  { to: '/owner/menu', label: 'Menu' },
  { to: '/owner/settings', label: 'Settings' },
];

// variant 'inline' drops the strip's own rule and bottom margin so it can sit on a row the page
// already draws. The order board uses it to put these links beside the status tabs: the board
// fits exactly six Preparing cards on a 1024x768 iPad, and a strip of its own would cost 55px,
// which is three of them.
export default function OwnerNav({ variant = 'block' }) {
  return (
    <nav className={`owner-nav owner-nav--${variant}`} aria-label="Owner sections">
      {SECTIONS.map((section) => (
        <NavLink
          key={section.to}
          to={section.to}
          end={section.end}
          className={({ isActive }) =>
            isActive ? 'owner-nav__link owner-nav__link--active' : 'owner-nav__link'
          }
        >
          {section.label}
        </NavLink>
      ))}
    </nav>
  );
}
