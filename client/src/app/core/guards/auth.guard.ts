import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Requires a signed-in user (own login or the dashboard cookie). */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.token() ? true : inject(Router).parseUrl('/login');
};

/** Requires a signed-in user that has at least one role; a dashboard user without a role waits on the "no access" page. */
export const accessGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const r = inject(Router);
  if (!auth.token()) return r.parseUrl('/login');
  return auth.hasAccess() ? true : r.parseUrl('/no-access');
};

/** A page that needs one permission (for example the roles screen needs "roles.manage"). */
export const permissionGuard = (code: string): CanActivateFn => () => {
  const auth = inject(AuthService);
  return auth.can(code) ? true : inject(Router).parseUrl('/home');
};

/** The address "/": the home page (3 pictures) once per browser session, then the first page the user may open. */
export const homeRedirect: CanActivateFn = () => {
  const auth = inject(AuthService);
  const r = inject(Router);
  if (!auth.token()) return r.parseUrl('/login');
  if (!auth.hasAccess()) return r.parseUrl('/no-access');
  if (!auth.homepageSeen()) return r.parseUrl('/home');
  return r.parseUrl(auth.can('files.view') ? '/files' : '/home');
};

/** The login page is only for visitors; a signed-in user goes on to the app. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.token() ? inject(Router).parseUrl('/') : true;
};
