import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom, tap } from 'rxjs';
import { API, AUTH_API_URL, DASHBOARD_URL } from '../config/api.config';
import { AccessInfo, LoginResponse, Self } from '../models';

const TOKEN_KEY = 'dd_token';
/** token placeholder when the browser is signed in through the dashboard cookie (the real token is HttpOnly and never visible here) */
export const COOKIE_TOKEN = '__cookie__';
export const HOMEPAGE_SEEN_KEY = 'dd_homepage_seen';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly user = signal<Self | null>(null);
  /** the dashboard (cookie) sign-in is on, and where the dashboard lives (for "sign in again" and sign-out) */
  readonly dashboardMode = signal(false);
  readonly dashboardUrl = signal('');
  /** permission codes of the signed-in user, from /authorization/me */
  readonly permissions = signal<string[]>([]);
  readonly permissionsLoaded = signal(false);
  readonly access = signal<AccessInfo | null>(null);

  readonly isLoggedIn = computed(() => !!this.token());
  readonly cookieSession = computed(() => this.token() === COOKIE_TOKEN);
  readonly role = computed(() => this.user()?.role ?? '');
  readonly isAdmin = computed(() => this.role() === 'Admin');
  /** a signed-in user whose roles give no access yet (for example a dashboard user an admin has not given a role) */
  readonly hasAccess = computed(() => this.role() === 'Admin' || this.role() === 'User');
  readonly displayName = computed(() => this.user()?.fullName || this.user()?.username || '');
  readonly initials = computed(() => (this.displayName() || '?').split(/\s+/).map(p => p[0]).slice(0, 2).join('').toUpperCase());

  /** true until the permissions are known (so a menu never flashes empty); the server checks every request anyway */
  can(code: string) { return !this.permissionsLoaded() || this.permissions().includes(code); }

  /**
   * Runs before the app starts. When the dashboard sign-in is on and the browser has the dashboard cookie, the user is signed in
   * without any login page (the server reads the cookie); otherwise DataDiode's own login is used as before.
   */
  async init() {
    try {
      const cfg = await firstValueFrom(this.http.get<{ dashboardMode: boolean; dashboardUrl: string }>(`${API}/auth/config`));
      this.dashboardMode.set(!!cfg.dashboardMode || !!DASHBOARD_URL);   // a DASHBOARD_URL in api.config.ts alone turns dashboard mode on
      this.dashboardUrl.set(DASHBOARD_URL || cfg.dashboardUrl || '');

      if (this.dashboardMode() && (!this.token() || this.token() === COOKIE_TOKEN)) {
        try {
          let me: Self;
          try { me = await firstValueFrom(this.http.get<Self>(`${API}/auth/me`)); }
          catch (e) {
            // the access token may just have expired: ask the dashboard to renew the session, then try once more
            if (!(await this.refreshDashboard())) throw e;
            me = await firstValueFrom(this.http.get<Self>(`${API}/auth/me`));
          }
          localStorage.removeItem(TOKEN_KEY);
          this.token.set(COOKIE_TOKEN);
          this.user.set(me);
        } catch {
          // no valid dashboard session
          if (this.token() === COOKIE_TOKEN) { this.token.set(null); this.user.set(null); }
        }
      } else if (this.token() && this.token() !== COOKIE_TOKEN) {
        // an own login token kept from an earlier visit: the server says whether it is still good
        try { this.user.set(await firstValueFrom(this.http.get<Self>(`${API}/auth/me`))); }
        catch { localStorage.removeItem(TOKEN_KEY); this.token.set(null); this.user.set(null); }
      }
    } catch { /* the server is not reachable yet: the normal pages will show the error */ }

    // dashboard mode and no valid session: go to the configured URL (Auth:DashboardUrl) instead of showing the login page
    if (!this.token() && this.dashboardMode() && this.dashboardUrl()) {
      window.location.href = this.dashboardUrl();
      return;
    }
    if (this.token()) await this.refreshAccess();
  }

  private refreshing: Promise<boolean> | null = null;

  /**
   * Asks the dashboard to renew the session: POST {AUTH_API_URL}/Auth/refresh with its refresh cookie (the server renews the cookie).
   * fetch is used on purpose, so the DataDiode-only header from the interceptor is not sent to the dashboard. One call at a time.
   */
  refreshDashboard(): Promise<boolean> {
    if (!AUTH_API_URL) return Promise.resolve(false);
    return this.refreshing ??= fetch(`${AUTH_API_URL}/Auth/refresh`, { method: 'POST', credentials: 'include' })
      .then(r => r.ok, () => false)
      .finally(() => { this.refreshing = null; });
  }

  login(username: string, password: string) {
    return this.http.post<LoginResponse>(`${API}/auth/login`, { username, password }).pipe(
      tap(r => {
        localStorage.setItem(TOKEN_KEY, r.token);
        try { sessionStorage.removeItem(HOMEPAGE_SEEN_KEY); } catch { /* storage blocked */ }
        this.token.set(r.token);
        this.user.set(r.user);
      }));
  }

  loadMe() { return this.http.get<Self>(`${API}/auth/me`).pipe(tap(u => this.user.set(u))); }

  /** Asks the server what the current token allows (roles + permission codes); a failure never blocks the app. */
  async refreshAccess() {
    if (!this.token()) { this.access.set(null); return null; }
    try {
      const info = await firstValueFrom(this.http.get<AccessInfo>(`${API}/authorization/me`));
      this.access.set(info);
      this.permissions.set(info.permissions ?? []);
      this.permissionsLoaded.set(true);
      return info;
    } catch {
      this.access.set(null);
      return null;
    }
  }

  logout(redirect = true) {
    localStorage.removeItem(TOKEN_KEY);
    try { sessionStorage.removeItem(HOMEPAGE_SEEN_KEY); } catch { /* storage blocked */ }
    this.token.set(null);
    this.user.set(null);
    this.access.set(null);
    this.permissions.set([]);
    this.permissionsLoaded.set(false);
    if (redirect && this.dashboardMode() && this.dashboardUrl()) { window.location.href = this.dashboardUrl(); return; }
    if (redirect) this.router.navigateByUrl('/login');
  }

  /** the home page (3 pictures) opens first, once per browser session */
  homepageSeen() { try { return sessionStorage.getItem(HOMEPAGE_SEEN_KEY) === '1'; } catch { return true; } }
  markHomepageSeen() { try { sessionStorage.setItem(HOMEPAGE_SEEN_KEY, '1'); } catch { /* storage blocked: it just shows again next time */ } }
}
