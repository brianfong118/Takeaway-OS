import { NavLink } from 'react-router-dom';
import './AccountNav.css';

const SECTIONS = [
  // `end` on Profile only: "/account" is a prefix of the other two paths, so without it the
  // Profile tab would stay lit on all three.
  { to: '/account', label: 'Profile', end: true },
  { to: '/account/addresses', label: 'Addresses' },
  { to: '/account/orders', label: 'Orders' },
];

export default function AccountNav() {
  return (
    <nav className="account-nav" aria-label="Account sections">
      {SECTIONS.map((section) => (
        <NavLink
          key={section.to}
          to={section.to}
          end={section.end}
          className={({ isActive }) =>
            isActive ? 'account-nav__link account-nav__link--active' : 'account-nav__link'
          }
        >
          {section.label}
        </NavLink>
      ))}
    </nav>
  );
}
