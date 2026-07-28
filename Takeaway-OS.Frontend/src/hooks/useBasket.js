import { useContext } from 'react';
import { BasketContext } from '../context/BasketContext.js';

// The only way components read the basket. Wrapping useContext here means no component has to
// know which context object holds it, and the missing-provider mistake fails loudly at the
// point of use rather than as "cannot read property of null" further down.
export function useBasket() {
  const basket = useContext(BasketContext);

  if (basket === null) {
    throw new Error('useBasket must be used inside a <BasketProvider>.');
  }

  return basket;
}
