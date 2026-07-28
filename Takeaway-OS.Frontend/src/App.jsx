import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout.jsx';
import MenuPage from './pages/MenuPage.jsx';
import MenuItemPage from './pages/MenuItemPage.jsx';
import BasketPage from './pages/BasketPage.jsx';
import NotFoundPage from './pages/NotFoundPage.jsx';

// The app's route table. Kept in one file so every URL the app answers to is visible at a glance.
export default function App() {
  return (
    <Routes>
      {/* No path -> a layout route. Contributes no URL segment, only the shared header/nav
          that its children render inside (via Layout's <Outlet />). */}
      <Route element={<Layout />}>
        <Route index element={<MenuPage />} /> {/* index = the parent's own path, so "/" */}

        {/* ":id" is a URL parameter, read back inside the page with useParams(). */}
        <Route path="menu/:id" element={<MenuItemPage />} />
        <Route path="basket" element={<BasketPage />} />

        {/* Inside the layout on purpose -> an unknown URL still gets the nav to escape with. */}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
