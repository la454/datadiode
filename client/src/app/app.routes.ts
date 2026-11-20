import { Routes } from '@angular/router';
import { accessGuard, authGuard, guestGuard, homeRedirect, permissionGuard } from './core/guards/auth.guard';
import { Login } from './features/auth/login';
import { NoAccess } from './features/auth/no-access';
import { Shell } from './layout/shell';

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], component: Login, title: 'Sign in · DataDiode' },
  { path: 'no-access', canActivate: [authGuard], component: NoAccess, title: 'No access · DataDiode' },

  // the app frame (black top navbar + black right pane); every page below needs a signed-in user that has a role
  {
    path: '',
    component: Shell,
    canActivate: [accessGuard],
    children: [
      { path: '', pathMatch: 'full', canActivate: [homeRedirect], children: [] },
      { path: 'home', loadComponent: () => import('./features/home/home').then(m => m.Home), title: 'Home · DataDiode' },
      { path: 'files', canActivate: [permissionGuard('files.view')], loadComponent: () => import('./features/files/files').then(m => m.Files), title: 'My files · DataDiode' },
      { path: 'users', canActivate: [permissionGuard('users.manage')], loadComponent: () => import('./features/admin/admin').then(m => m.Admin), title: 'Users & groups · DataDiode' },
      { path: 'roles', canActivate: [permissionGuard('roles.manage')], loadComponent: () => import('./features/roles/roles').then(m => m.Roles), title: 'Roles & permissions · DataDiode' },
      { path: 'user-roles', canActivate: [permissionGuard('userroles.manage')], loadComponent: () => import('./features/user-roles/user-roles').then(m => m.UserRoles), title: 'Users & roles · DataDiode' },
      { path: 'audit', canActivate: [permissionGuard('audit.view')], loadComponent: () => import('./features/audit/audit').then(m => m.Audit), title: 'Audit log · DataDiode' },
      { path: 'admin', redirectTo: 'users' },      // the old address of the user management page
    ],
  },
  { path: '**', redirectTo: '' },
];
