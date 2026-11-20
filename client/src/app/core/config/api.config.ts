/**
 * Base URL of the C# API (see server/).
 *  - Served by the API itself or by the dashboard on the same domain (the normal case once it lives inside the dashboard): the API is
 *    "api" next to the page, so the HttpOnly dashboard cookie is sent automatically and no cross-site call is made. This also follows
 *    the <base href> when the app lives under a path such as /datadiode/.
 *  - Angular dev server (ports 4200 / 4300): the API runs on port 5233 of the same host (localhost, IP or PC name).
 *  - Anything else (the built files served by another web server on its own port): the API on port 5233 of the same host, as before.
 *  Set API_OVERRIDE to a full address (for example 'https://dashboard.corp.com/datadiode/api') to force one.
 */
export const API_OVERRIDE = '';

const port = typeof location !== 'undefined' ? location.port : '';
const sameOrigin = port === '5233' || port === '7083';
const isDevServer = port === '4200' || port === '4300';

function detect(): string {
  if (typeof location === 'undefined' || typeof document === 'undefined') return 'http://localhost:5233/api';
  const next = () => new URL('api', document.baseURI).href.replace(/\/$/, '');
  const onApiPort = () => `${location.protocol}//${location.hostname}:5233/api`;
  if (isDevServer) return onApiPort();
  // served by the API itself, or through the dashboard / a reverse proxy on the default port (80 / 443): the API sits next to the page
  if (sameOrigin || port === '') return next();
  // a plain static host on some other port: keep the old behaviour (the API on 5233 of the same machine)
  return onApiPort();
}

export const API = API_OVERRIDE || detect();

/**
 * Where users sign in (the dashboard login page), full address with http(s)://, e.g. 'https://dashboard.corp.com/login'.
 * Used when there is no valid dashboard session and after sign-out. Leave '' to use Auth:DashboardUrl from the server's appsettings.json.
 */
export const DASHBOARD_URL = '';

/**
 * Base address of the dashboard's auth API, e.g. 'https://dashboard.corp.com/api' (the app calls POST {AUTH_API_URL}/Auth/refresh with the cookie).
 * When the access token has expired this renews the session silently instead of sending the user to the dashboard. Leave '' to turn it off.
 */
export const AUTH_API_URL = '';
