// Reading a JWT's claims in the browser, for RENDERING only.

const ROLE_CLAIM_URI = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

// base64url -> base64, then bytes -> a real UTF-8 string.
function decodeBase64Url(segment) {
  const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');  
  const binary = atob(base64);
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

// token -> { sub, email, role, exp } or null if it is not a readable JWT at all.
// Returns null rather than throwing: every caller's response to a bad token is the same
// (treat it as logged out), and the token comes from localStorage, which a user can hand-edit.
export function decodeJwt(token) {
  if (typeof token !== 'string') return null;

  const parts = token.split('.'); // header.payload.signature
  if (parts.length !== 3) return null;

  try {
    const claims = JSON.parse(decodeBase64Url(parts[1])); // [1] = payload; the signature is not ours to check

    return {
      sub: claims.sub,     // the ApplicationUser id, written by JwtRegisteredClaimNames.Sub
      email: claims.email,
      role: claims[ROLE_CLAIM_URI] ?? claims.role, // long URI first, short name as the fallback
      exp: claims.exp,     // seconds since the Unix epoch, NOT milliseconds - see isExpired
    };
  } catch {
    // Covers a payload that is not valid base64, not valid JSON, or not an object.
    return null;
  }
}

// A token with no exp counts as expired. Ours always has one (GenerateJwt sets a 1-day expiry),
// so its absence means the token is not one of ours and should not be treated as valid.
export function isExpired(claims) {
  if (!claims || typeof claims.exp !== 'number') return true;
  return claims.exp * 1000 <= Date.now();
}
