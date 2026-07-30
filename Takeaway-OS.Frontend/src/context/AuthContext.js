import { createContext } from 'react';

// Own file for the same reason as BasketContext: fast refresh wants a module to export
// components or plain values, not both.
export const AuthContext = createContext(null);
