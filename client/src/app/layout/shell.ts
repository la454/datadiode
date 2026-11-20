import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/services';

interface NavItem { path: string; label: string; icon: string; need?: string; }
interface NavGroup { title: string; icon: string; items: NavItem[]; }

function saved(key: string): string | null {
  try { return localStorage.getItem(key); } catch { return null; }
}
function save(key: string, value: string) {
  try { localStorage.setItem(key, value); } catch { /* storage blocked: the choice just is not remembered */ }
}

/**
 * The app frame: a black top navbar and a black navigation pane on the right. The menu shows only what the signed-in user's
 * permissions allow (the server checks the same permission on every request).
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="dd-topbar">
      <button type="button" class="dd-tb-btn dd-tb-menu" (click)="open.set(true)" aria-label="Open menu"><i class="bi bi-list"></i></button>
      <a class="dd-brand" routerLink="/home" title="Home">
        <img src="logo.png" width="42" height="42" alt="">
        <span class="dd-brand-t"><b lang="ar" dir="rtl">معبر آمن</b><small>SECURE DATA TRANSFER</small></span>
      </a>
      <span class="dd-sp"></span>
      <span class="dd-secure d-none d-lg-inline-flex"><i class="bi bi-shield-lock"></i>One-way secure transfer</span>
      <span class="dd-user">
        <span class="dd-av">{{ auth.initials() }}</span>
        <span class="dd-user-t"><b>{{ auth.displayName() }}</b><small>{{ roleLabel() }}</small></span>
      </span>
      <button type="button" class="dd-tb-btn dd-logout" (click)="auth.logout()" title="Sign out" aria-label="Sign out"><i class="bi bi-box-arrow-right"></i></button>
    </header>

    <div class="dd-layout" [class.mini]="collapsed()">
      <main class="dd-main" [class.full]="isHome()"><router-outlet /></main>

      <aside class="dd-pane" [class.open]="open()" aria-label="Navigation">
        <div class="dd-hello"><small>Welcome back</small><b>{{ firstName() }}</b></div>
        <div class="dd-status">
          <span class="dd-pulse"></span>
          <span class="dd-status-t">
            <b>Secure session</b>
            <small>{{ auth.user()?.fromDashboard ? 'Signed in through the dashboard' : 'Encrypted · access is logged' }}</small>
          </span>
        </div>

        <nav (click)="open.set(false)">
          @for (g of visible(); track g.title) {
            <div class="dd-sec" [class.shut]="isShut(g.title)">
              <button type="button" class="dd-group" (click)="toggleGroup(g.title); $event.stopPropagation()"
                      [attr.aria-expanded]="!isShut(g.title)" [attr.title]="collapsed() ? g.title : null">
                <span class="dd-gicon"><i class="bi bi-{{ g.icon }}"></i></span>
                <span class="dd-gtitle">{{ g.title }}</span>
                <span class="dd-gcount">{{ g.items.length }}</span>
                <i class="dd-chev" aria-hidden="true"></i>
              </button>
              <div class="dd-items"><div class="dd-items-in">
                @for (i of g.items; track i.path) {
                  <a [routerLink]="i.path" routerLinkActive="active" [attr.title]="collapsed() ? i.label : null">
                    <span class="dd-ico"><i class="bi bi-{{ i.icon }}"></i></span><span class="dd-lbl">{{ i.label }}</span>
                  </a>
                }
              </div></div>
            </div>
          }
        </nav>

        <button type="button" class="dd-fold" (click)="toggleCollapsed()" [attr.title]="collapsed() ? 'Expand the menu' : 'Collapse the menu'">
          <span class="dd-ico"><i class="bi" [class.bi-layout-sidebar-inset-reverse]="!collapsed()" [class.bi-layout-sidebar-reverse]="collapsed()"></i></span>
          <span class="dd-lbl">Collapse menu</span>
        </button>

        <div class="dd-me">
          <span class="dd-av lg">{{ auth.initials() }}</span>
          <div class="dd-me-t">
            <b class="text-truncate">{{ auth.displayName() }}</b>
            @if (auth.user()?.email; as mail) { <small class="text-truncate">{{ mail }}</small> }
            <span class="dd-role">{{ roleLabel() }}</span>
          </div>
        </div>
      </aside>
      @if (open()) { <div class="dd-scrim" (click)="open.set(false)"></div> }
    </div>
  `,
})
export class Shell {
  auth = inject(AuthService);
  private router = inject(Router);

  open = signal(false);
  collapsed = signal(saved('dd_sb_mini') === '1');
  private url = signal(this.router.url);
  isHome = computed(() => this.url().split('?')[0] === '/home');

  private shut = signal<Set<string>>(this.savedShut());
  isShut(title: string) { return this.shut().has(title) && !this.collapsed(); }
  toggleGroup(title: string) {
    if (this.collapsed()) return;
    const next = new Set(this.shut());
    if (!next.delete(title)) next.add(title);
    this.shut.set(next);
    save('dd_sb_shut', JSON.stringify([...next]));
  }
  toggleCollapsed() {
    const v = !this.collapsed();
    this.collapsed.set(v);
    save('dd_sb_mini', v ? '1' : '0');
  }
  private savedShut(): Set<string> {
    try { return new Set<string>(JSON.parse(saved('dd_sb_shut') ?? '[]')); } catch { return new Set<string>(); }
  }

  readonly firstName = computed(() => (this.auth.displayName().trim().split(/\s+/)[0]) || '');
  readonly roleLabel = computed(() => {
    const roles = this.auth.access()?.roles ?? [];
    return roles.length ? roles.join(' · ') : (this.auth.role() || 'No role');
  });

  private readonly groups: NavGroup[] = [
    { title: 'Overview', icon: 'grid-1x2', items: [
      { path: '/home', label: 'Home', icon: 'house-door' },
      { path: '/files', label: 'My files', icon: 'folder2-open', need: 'files.view' },
    ] },
    { title: 'Administration', icon: 'people', items: [
      { path: '/users', label: 'Users & groups', icon: 'people', need: 'users.manage' },
    ] },
    { title: 'Security', icon: 'shield-lock', items: [
      { path: '/roles', label: 'Roles & permissions', icon: 'shield-check', need: 'roles.manage' },
      { path: '/user-roles', label: 'Users & roles', icon: 'person-gear', need: 'userroles.manage' },
      { path: '/audit', label: 'Audit log', icon: 'journal-text', need: 'audit.view' },
    ] },
  ];

  /** the menu the signed-in user may see: items without permission are hidden, empty sections disappear */
  readonly visible = computed(() => this.groups
    .map(g => ({ ...g, items: g.items.filter(i => !i.need || this.auth.can(i.need)) }))
    .filter(g => g.items.length > 0));

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe(e => {
      if (e instanceof NavigationEnd) { this.url.set(e.urlAfterRedirects); this.open.set(false); }
    });
  }
}
