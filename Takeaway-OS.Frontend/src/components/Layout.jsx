import { Link, NavLink, Outlet } from 'react-router-dom';
import { useBasket } from '../hooks/useBasket.js';
import { useAuth } from '../hooks/useAuth.js';
import { ROLES } from '../api/auth.js';
import './Layout.css';

// Shared chrome for every customer-facing page. Rendered by the layout route in App.jsx,
// so pages themselves contain only their own content.
export default function Layout() {
  const { itemCount } = useBasket();
  const { user, isLoggedIn, logOut } = useAuth();

  // Returned by NavLink's className function on every navigation -> keeps the active-state
  // logic in one place instead of repeating the ternary on each link.
  const linkClass = ({ isActive }) =>
    isActive ? 'layout__link layout__link--active' : 'layout__link';

  // Staff are here to work, not to order, so their dashboard link REPLACES Menu and Basket
  // rather than joining them. Also what keeps the bar from overflowing on a phone, where
  // "Deliveries" plus the role badge plus Log out already fills the width.
  // The brand still links to "/", so the menu stays one tap away.
  const isStaff = user?.role === ROLES.Owner || user?.role === ROLES.Driver;

  return (
    <div className="layout">
      <header className="layout__header">
        <div className="layout__bar">
          {/* Plain Link, not NavLink -> the brand is a way home, never a highlighted tab. */}
          <Link to="/" className="layout__brand">
            Takeaway<span className="layout__brand-accent">OS</span>
          </Link>

          <nav aria-label="Main"> {/* names the landmark, so screen readers can distinguish it from other navs later */}
            <ul className="layout__nav">
              {!isStaff && (
                <>
                  <li>
                    {/* end -> exact match only. Without it "/" prefixes every URL and stays active everywhere. */}
                    <NavLink to="/" end className={linkClass}>
                      Menu
                    </NavLink>
                  </li>
                  <li>
                    <NavLink to="/basket" className={linkClass}>
                      Basket
                      {/* Explicit > 0: a bare `itemCount &&` would render a literal 0 when empty. */}
                      {itemCount > 0 && (
                        <span className="layout__badge" aria-label={`${itemCount} items in basket`}>
                          {itemCount}
                        </span>
                      )}
                    </NavLink>
                  </li>
                </>
              )}
              {/* Decides which link to DRAW, never what data comes back - the API is the guard. */}
              {/* ONE link, not one per owner screen. Measured: Orders + Menu took this bar to
                  392px against a 375px phone, and a third would have reached ~477px. The section
                  switcher moved into the page (OwnerNav), so this stays fixed-width however many
                  owner screens exist. No `end` here on purpose - unlike the tabs inside the page,
                  this link SHOULD stay lit across every /owner/* URL. */}
              {user?.role === ROLES.Owner && (
                <li>
                  <NavLink to="/owner" className={linkClass}>
                    Admin
                  </NavLink>
                </li>
              )}
              {user?.role === ROLES.Driver && (
                <li>
                  <NavLink to="/driver" className={linkClass}>
                    Deliveries
                  </NavLink>
                </li>
              )}
              {isLoggedIn ? (
                <li className="layout__account">
                  <span className="layout__role">{user.role}</span>
                  <span className="layout__email">{user.email}</span>
                  {/* No navigate() afterwards: ProtectedRoute already redirects a logged-out
                      user off a guarded page, and on a public page there is nowhere to send them. */}
                  <button type="button" className="layout__logout" onClick={logOut}>
                    Log out
                  </button>
                </li>
              ) : (
                <li>
                  <NavLink to="/login" className={linkClass}>
                    Log in
                  </NavLink>
                </li>
              )}
            </ul>
          </nav>
        </div>
      </header>

      <main className="layout__main">
        <Outlet /> {/* the matched child route renders here */}
      </main>

      <footer className="layout__footer">
        <p>&copy; {new Date().getFullYear()} TakeawayOS</p>
      </footer>
    </div>
  );
}

