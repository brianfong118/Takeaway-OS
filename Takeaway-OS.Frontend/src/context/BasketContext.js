import { createContext } from 'react';

// Alone in its own file because Vite's fast refresh only works cleanly when a module exports
// either components or plain values, not both.
// null default: useBasket() reads it as "no provider above me" and throws.
export const BasketContext = createContext(null);
