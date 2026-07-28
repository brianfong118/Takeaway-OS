import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import './index.css'; // imported here rather than in a component -> app-wide tokens and resets
import App from './App.jsx';
import { BasketProvider } from './context/BasketProvider.jsx';

// The one place React attaches to the page. #root is the empty div in index.html.
createRoot(document.getElementById('root')).render(
  // BrowserRouter must sit above App so everything beneath it can read the current URL and
  // render Link/Outlet. Nothing outside it may use router features.
  <StrictMode>
    <BrowserRouter>
      {/* Above App so any page can read the basket, inside BrowserRouter so it may use Link. */}
      <BasketProvider>
        <App />
      </BasketProvider>
    </BrowserRouter>
  </StrictMode>,
);
