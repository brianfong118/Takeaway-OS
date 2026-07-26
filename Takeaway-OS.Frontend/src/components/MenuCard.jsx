import { Link } from 'react-router-dom';
import { formatPrice } from '../utils/format.js';
import './MenuCard.css';

// Presentational: takes one item from GET /api/menuitems and renders it. No fetching, no state.
export default function MenuCard({ item }) {
  return (
    <li className="menu-card">
      {/* The whole card is the link, so the tap target is the card and not just the name. */}
      <Link to={`/menu/${item.id}`} className="menu-card__link">
        <div className="menu-card__header">
          <h3 className="menu-card__name">{item.name}</h3>
          <span className="menu-card__price">{formatPrice(item.price)}</span>
        </div>
        {/* Description is optional in the DB , render nothing rather than an empty <p>. */}
        {item.description && <p className="menu-card__description">{item.description}</p>}
      </Link>
    </li>
  );
}
