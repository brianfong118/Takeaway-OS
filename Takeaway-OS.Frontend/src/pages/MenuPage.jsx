import { useEffect, useState } from 'react';
import { getCategories } from '../api/categories.js';
import { getMenuItems } from '../api/menu.js';
import MenuCard from '../components/MenuCard.jsx';
import './MenuPage.css';

export default function MenuPage() {
  const [categories, setCategories] = useState([]);
  const [items, setItems] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    // Flipped by the cleanup below. Guards every setState so a response that arrives after
    // this component is gone cannot update state that no longer exists.
    let ignore = false;

    async function load() {
      try {
        // Independent requests, so they go out together instead of one after the other.
        const [categoryList, itemList] = await Promise.all([getCategories(), getMenuItems()]);
        if (ignore) return;
        setCategories(categoryList);
        setItems(itemList);
      } catch (err) {
        if (!ignore) setError(err.message);
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, []); // empty deps, so this runs once when the page mounts

  // Derived from state on every render, not stored in state. Keeping a copy in state would
  // give two sources of truth that can drift apart.
  const sections = categories
    .map((category) => ({
      category,
      items: items.filter((item) => item.categoryId === category.id),
    }))
    .filter((section) => section.items.length > 0); // hide a category with nothing available

  if (isLoading) {
    return <p className="menu__state">Loading menu...</p>;
  }

  if (error) {
    return (
      <div className="menu__state menu__state--error" role="alert">
        <p>Could not load the menu.</p>
        <p className="menu__state-detail">{error}</p>
      </div>
    );
  }

  if (sections.length === 0) {
    return <p className="menu__state">Nothing is available to order right now.</p>;
  }

  return (
    <div className="menu">
      <h1 className="menu__title">Menu</h1>

      {/* Cross-contamination warning, required alongside the per-dish allergen text that lives in
          each item's description*/}
      <p className="menu__allergens">
        All dishes are prepared in a kitchen that handles peanuts, tree nuts, gluten, shellfish,
        sesame and soya, so we cannot guarantee any dish is free from traces of these.
      </p>

      {sections.map(({ category, items: categoryItems }) => (
        <section key={category.id} className="menu__section">
          <h2 className="menu__category">{category.name}</h2>

          <ul className="menu__grid">
            {categoryItems.map((item) => (
              <MenuCard key={item.id} item={item} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
