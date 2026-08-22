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

  // The free hosting tier stops the API when nobody is using it, so the first request after an
  // idle spell waits for a container to boot - roughly a minute. fetch has no timeout of its own
  const [isSlow, setIsSlow] = useState(false);

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

  // On a DELAY rather than shown immediately: a warm server answers in well under a second, and
  // explaining a wait that is not happening is its own kind of noise.
  //
  // clearTimeout for the same reason the confirmation page clears its interval - nothing owns a
  // browser timer, so unmounting does not stop it, and the callback would set state on a component
  // that is gone. Depending on isLoading means a fast response cancels the timer before it fires.
  useEffect(() => {
    if (!isLoading) return;

    const timerId = setTimeout(() => setIsSlow(true), 3000);
    return () => clearTimeout(timerId);
  }, [isLoading]);

  // Derived from state on every render, not stored in state. Keeping a copy in state would
  // give two sources of truth that can drift apart.
  const sections = categories
    .map((category) => ({
      category,
      items: items.filter((item) => item.categoryId === category.id),
    }))
    .filter((section) => section.items.length > 0); // hide a category with nothing available

  if (isLoading) {
    return (
      <div className="menu__state">
        <p>Loading menu...</p>
        {isSlow && (
          <p className="menu__state-detail">
            This demo runs on a free server that sleeps when nobody is using it, so the first load
            can take up to a minute. Everything is quick once it has woken up.
          </p>
        )}
      </div>
    );
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
