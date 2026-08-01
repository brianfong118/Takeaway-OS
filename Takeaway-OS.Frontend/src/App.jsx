import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout.jsx';
import MenuPage from './pages/MenuPage.jsx';
import MenuItemPage from './pages/MenuItemPage.jsx';
import BasketPage from './pages/BasketPage.jsx';
import CheckoutPage from './pages/CheckoutPage.jsx';
import PaymentPage from './pages/PaymentPage.jsx';
import OrderConfirmationPage from './pages/OrderConfirmationPage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import OwnerDashboardPage from './pages/OwnerDashboardPage.jsx';
import DriverDashboardPage from './pages/DriverDashboardPage.jsx';
import NotFoundPage from './pages/NotFoundPage.jsx';
import ProtectedRoute from './components/ProtectedRoute.jsx';
import { ROLES } from './api/auth.js';

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
        <Route path="checkout" element={<CheckoutPage />} />

        {/* Reached only by CheckoutPage navigating here after an order is created. Still a
            normal, refreshable URL: the order id and client secret come from sessionStorage,
            not from navigation state, so reloading /pay keeps working. */}
        <Route path="pay" element={<PaymentPage />} />

        {/* The guest's permanent link to one order. The token in the URL is what authorises the
            read, so unlike /pay this page needs no sessionStorage at all - it works from a
            bookmark, in a new tab, or days later. */}
        <Route path="order/:token" element={<OrderConfirmationPage />} />

        {/* Owner and Driver sign in here; customers never have to. ProtectedRoute sends anyone
            who hits a guarded URL logged-out to this path, with where they came from attached. */}
        <Route path="login" element={<LoginPage />} />

        {/* Another pathless layout route, this one contributing a guard instead of chrome.
            Children render through ProtectedRoute's <Outlet />, so one wrapper covers all of
            them. UI-only: every endpoint these pages call is [Authorize]d server-side too. */}
        <Route element={<ProtectedRoute roles={[ROLES.Owner]} />}>
          <Route path="owner" element={<OwnerDashboardPage />} />
        </Route>

        <Route element={<ProtectedRoute roles={[ROLES.Driver]} />}>
          <Route path="driver" element={<DriverDashboardPage />} />
        </Route>

        {/* Inside the layout on purpose -> an unknown URL still gets the nav to escape with. */}
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
