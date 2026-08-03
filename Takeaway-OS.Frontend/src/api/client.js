// VITE_ vars are baked into the bundle the browser downloads, so they are public.
// Never put a secret here (this is why only Stripe's *publishable* key is a VITE_ var).
const API_URL = import.meta.env.VITE_API_URL;

// Fail at startup instead of quietly requesting "undefined/api/menuitems" later.
if (!API_URL) {
  throw new Error('VITE_API_URL is not set. Add it to .env.local and restart the dev server.');
}

// Prefixed so it can't collide with anything else on this origin (the basket adds its own key).
const TOKEN_KEY = 'takeawayos.token';

export function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token) {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY);
}

// Set by AuthProvider so it hears about a token this layer rejects. Without it, clearToken()
// below empties localStorage while React state still says "logged in as Owner".
// A single slot, not a list of listeners: only one thing in the app owns who is logged in.
let onUnauthorized = null;

export function setUnauthorizedHandler(handler) {
  onUnauthorized = handler;
}


export class ApiError extends Error {
  constructor(message, status, body) {
    super(message);
    this.name = 'ApiError';
    this.status = status; // HTTP status code or 0 for network failure (no HTTP response at all)
    this.body = body;     // parsed response body, for callers needing more than the message
  }
}

function extractErrorMessage(body, status) {
  if (typeof body === 'string' && body.length > 0) return body;

  if (body && typeof body === 'object') {
    if (body.errors) {
      const messages = Object.values(body.errors).flat(); 
      if (messages.length > 0) return messages.join(' ');
    }
    if (body.detail) return body.detail;
    if (body.title) return body.title;
  }
  return `Request failed with status ${status}.`;
}


async function request(path, { method = 'GET', body, auth = true } = {}) {
  const headers = {};

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  const token = auth ? getToken() : null;
  if (token) {
    headers.Authorization = `Bearer ${token}`; // "Bearer " prefix is required by the JWT scheme in Program.cs
  }

  let response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,    
      body: body === undefined ? undefined : JSON.stringify(body), 
    });
  } catch {
    throw new ApiError('Could not reach the server. Check your connection and try again.', 0, null);
  }

  if (response.status === 204) return null; 

  const contentType = response.headers.get('content-type') ?? '';
 
  const data = contentType.includes('json')
    ? await response.json()
    : await response.text();

  if (!response.ok) { 
    // `token` guards this so a failed login (auth: false, so nothing sent) can't log out the
    // session the user already had. Only a credential we actually sent gets binned.
    if (response.status === 401 && token) {
      clearToken();
      onUnauthorized?.();
    }
    throw new ApiError(extractErrorMessage(data, response.status), response.status, data);
  }

  return data;
}

export const api = {
  get: (path, options) => request(path, { ...options, method: 'GET' }),
  post: (path, body, options) => request(path, { ...options, method: 'POST', body }),
  put: (path, body, options) => request(path, { ...options, method: 'PUT', body }),
  del: (path, options) => request(path, { ...options, method: 'DELETE' }),
};
