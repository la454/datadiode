import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { API, AUTH_API_URL } from '../config/api.config';
import { AuthService, COOKIE_TOKEN } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API)) return next(req);      // only the DataDiode API gets the token, the header and the cookie
  const auth = inject(AuthService);
  const t = auth.token();
  // every request carries this header: a request that changes something and was sent by ANOTHER web site cannot add it,
  // so the cookie sign-in is protected against forged requests. A cookie session sends no Authorization header (the cookie does the work).
  let authed = req.clone({ setHeaders: { 'X-DataDiode-Request': '1' }, withCredentials: true });
  if (t && t !== COOKIE_TOKEN) authed = authed.clone({ setHeaders: { Authorization: `Bearer ${t}` } });
  return next(authed).pipe(
    catchError(err => {
      if (err.status === 401 && !req.url.includes('/auth/')) {
        // cookie session whose access token expired: renew it through the dashboard and repeat the call once
        if (auth.cookieSession() && AUTH_API_URL) {
          return from(auth.refreshDashboard()).pipe(switchMap(ok => {
            if (!ok) { auth.logout(); return throwError(() => err); }
            return next(authed).pipe(catchError(e2 => { if (e2.status === 401) auth.logout(); return throwError(() => e2); }));
          }));
        }
        auth.logout();
      }
      return throwError(() => err);
    })
  );
};
