import { Link, NavLink, Outlet } from 'react-router-dom';
import { useBasket } from '../hooks/useBasket.js';
import './Layout.css';

// Shared chrome for every customer-facing page. Rendered by the layout route in App.jsx,
// so pages themselves contain only their own content.
export default function Layout() {
  const { itemCount } = useBasket();

  // Returned by NavLink's className function on every navigation -> keeps the active-state
  // logic in one place instead of repeating the ternary on each link.
  const linkClass = ({ isActive }) =>
    isActive ? 'layout__link layout__link--active' : 'layout__link';

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
              <li>
                <NavLink to="/login" className={linkClass}>
                  Log in
                </NavLink>
              </li>
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

