import { useEffect, useMemo, useState } from 'react';
import { AuthContext } from './AuthContext.js';
import { clearToken, getToken, setToken, setUnauthorizedHandler } from '../api/client.js';
import { login as loginRequest, registerCustomer as registerRequest } from '../api/auth.js';
import { decodeJwt, isExpired } from '../utils/jwt.js';

// claims -> the shape the rest of the app consumes, so no component has to know a JWT is involved.
function toUser(claims) {
  return {
    id: claims.sub, // a STRING ("2"), since JWT claims are text - compare w/ String(order.customerId)
    email: claims.email,
    role: claims.role,
  };
}

function readStoredUser() {
  const token = getToken();
  const claims = decodeJwt(token);

  if (!claims || isExpired(claims)) {
    if (token) clearToken(); // a dead token would otherwise be attached to requests that can only 401
    return null;
  }

  return toUser(claims);
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(readStoredUser);

  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null));
    return () => setUnauthorizedHandler(null); // a discarded provider must not keep setting state
  }, []);

  const value = useMemo(() => {
    // Everything both entry points do once the API has handed back a token. Registering signs
    // the customer straight in - the API returns the same AuthResponse for register as for
    // login - so the two paths differ only in which request produced the token, and that
    // difference is the ONLY thing left in logIn/signUp below.
    //
    // Declared inside the factory rather than in the component body for the reason already
    // applied to logIn/logOut: a body-declared function is a new object every render, so it
    // would have to go in the dep array and would invalidate the memo on every render.
    function commitToken(token) {
      setToken(token);
      const claims = decodeJwt(token);
      if (!claims) {
        clearToken();
        throw new Error('The server returned a token that could not be read.');
      }

      const signedIn = toUser(claims);
      setUser(signedIn);
      return signedIn; // so the caller can redirect on role without waiting for a re-render
    }

    return {
      user,
      isLoggedIn: user !== null,

      // Throws ApiError on failure (401 = bad credentials). The caller shows the message;
      // this only commits a SUCCESSFUL login, so a failed attempt leaves the session untouched.
      logIn: async (email, password) => {
        const response = await loginRequest(email, password);
        return commitToken(response.token);
      },

      signUp: async (details) => {
        const response = await registerRequest(details);
        return commitToken(response.token);
      },

      logOut: () => {
        clearToken();
        setUser(null);
      },
    };
  }, [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
