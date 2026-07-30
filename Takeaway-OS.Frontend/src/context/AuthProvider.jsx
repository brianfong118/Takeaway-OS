import { useEffect, useMemo, useState } from 'react';
import { AuthContext } from './AuthContext.js';
import { clearToken, getToken, setToken, setUnauthorizedHandler } from '../api/client.js';
import { login as loginRequest } from '../api/auth.js';
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

  const value = useMemo(
    () => ({
      user,
      isLoggedIn: user !== null,

      // Throws ApiError on failure (401 = bad credentials). The caller shows the message;
      // this only commits a SUCCESSFUL login, so a failed attempt leaves the session untouched.
      logIn: async (email, password) => {
        const response = await loginRequest(email, password);
        setToken(response.token);

        // Read back from the token rather than from response.email/role, so what the UI shows
        // is what the API will actually act on. The two can only differ if something is wrong.
        const claims = decodeJwt(response.token);
        if (!claims) {
          clearToken();
          throw new Error('The server returned a token that could not be read.');
        }

        const loggedIn = toUser(claims);
        setUser(loggedIn);
        return loggedIn; // so the caller can redirect on role without waiting for a re-render
      },

      logOut: () => {
        clearToken();
        setUser(null);
      },
    }),
    [user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
