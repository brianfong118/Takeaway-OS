import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout.jsx';
import MenuPage from './pages/MenuPage.jsx';
import MenuItemPage from './pages/MenuItemPage.jsx';
import BasketPage from './pages/BasketPage.jsx';
import CheckoutPage from './pages/CheckoutPage.jsx';
import PaymentPage from './pages/PaymentPage.jsx';
import OrderConfirmationPage from './pages/OrderConfirmationPage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import RegisterPage from './pages/RegisterPage.jsx';
import AccountPage from './pages/AccountPage.jsx';
import AddressesPage from './pages/AddressesPage.jsx';
import OrderHistoryPage from './pages/OrderHistoryPage.jsx';
import OwnerDashboardPage from './pages/OwnerDashboardPage.jsx';
import OwnerMenuPage from './pages/OwnerMenuPage.jsx';
import OwnerSettingsPage from './pages/OwnerSettingsPage.jsx';
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

        {/* Its own URL rather than a tab on /login: password managers key their sign-in vs
            sign-up behaviour off the page, and a bookmark or a shared link should land on the
            form it names. Customer accounts only : Owner and Driver logins are created
            elsewhere (the first Owner by the self-closing bootstrap, Drivers by an Owner). */}
        <Route path="register" element={<RegisterPage />} />

        {/* Another pathless layout route, this one contributing a guard instead of chrome.
            Children render through ProtectedRoute's <Outlet />, so one wrapper covers all of
            them. UI-only: every endpoint these pages call is [Authorize]d server-side too. */}
        {/* Each owner page renders <OwnerNav /> itself rather than inheriting it from a layout
            route. The board needs it INSIDE its status-tab row (a strip of its own would cost
            55px, three Preparing cards on an iPad), and a layout route can only put it above. */}
        <Route element={<ProtectedRoute roles={[ROLES.Owner]} />}>
          <Route path="owner" element={<OwnerDashboardPage />} />
          <Route path="owner/menu" element={<OwnerMenuPage />} />
          <Route path="owner/settings" element={<OwnerSettingsPage />} />
        </Route>

        {/* The customer's own area. Guarded by role like the owner and driver sections, but
            note what that guard is NOT for: ordering never needs an account, so nothing behind
            here is on the path to placing an order. It is only the extras an account adds. */}
        <Route element={<ProtectedRoute roles={[ROLES.Customer]} />}>
          <Route path="account" element={<AccountPage />} />
          <Route path="account/addresses" element={<AddressesPage />} />
          <Route path="account/orders" element={<OrderHistoryPage />} />
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
