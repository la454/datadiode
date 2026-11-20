import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/services';
import { RoleName, UserRolesRow } from '../../core/models';
import { errMsg } from '../../core/utils/format';

/** Assign roles to users: a user can have many roles; a dashboard user with no role has no access yet. */
@Component({
  selector: 'app-user-roles',
  imports: [FormsModule],
  template: `
    <div class="dd-hero">
      <div class="dd-hero-ico"><i class="bi bi-person-gear"></i></div>
      <div class="dd-hero-t">
        <h1>Users &amp; roles</h1>
        <p>Give each user one or more roles. A user who came from the dashboard has no role at first and cannot open any page until you assign one.</p>
      </div>
    </div>

    @if (msg()) {
      <div class="alert alert-dismissible" [class.alert-danger]="isError()" [class.alert-success]="!isError()" role="alert">
        {{ msg() }}<button type="button" class="btn-close" (click)="msg.set('')"></button>
      </div>
    }

    <div class="card">
      <div class="card-header py-3 d-flex flex-wrap align-items-center gap-2">
        <div class="input-group search-wide">
          <span class="input-group-text bg-body"><i class="bi bi-search"></i></span>
          <input class="form-control" name="q" placeholder="Search by name, username or e-mail…" [(ngModel)]="search" (keyup.enter)="load()">
          <button type="button" class="btn btn-outline-primary" (click)="load()">Search</button>
        </div>
        <span class="ms-auto small text-body-secondary">{{ users().length }} users</span>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th>User</th><th>Source</th>
              @for (r of roles(); track r.roleId) { <th class="text-center">{{ r.name }}</th> }
            </tr>
          </thead>
          <tbody>
            @for (u of users(); track u.userId) {
              <tr [class.table-warning]="!u.roleIds.length && u.isActive" [class.opacity-50]="!u.isActive">
                <td>
                  <div class="fw-medium">{{ u.fullName }}
                    @if (!u.isActive) { <span class="badge text-bg-secondary ms-1">disabled</span> }
                    @if (!u.roleIds.length) { <span class="badge text-bg-warning ms-1">no role</span> }
                  </div>
                  <div class="small text-body-secondary">{{ u.username }}@if (u.email) { · {{ u.email }} }</div>
                </td>
                <td>@if (u.externalId) { <span class="badge text-bg-primary">Dashboard</span> } @else { <span class="badge text-bg-light border">Local</span> }</td>
                @for (r of roles(); track r.roleId) {
                  <td class="text-center">
                    <input class="form-check-input" type="checkbox" [checked]="has(u, r.roleId)" [disabled]="saving() === u.userId"
                           (change)="toggle(u, r.roleId)" [attr.aria-label]="r.name + ' for ' + u.username">
                  </td>
                }
              </tr>
            } @empty {
              <tr><td [attr.colspan]="roles().length + 2" class="text-center text-body-secondary py-5">
                <i class="bi bi-people fs-1 d-block mb-2"></i>No users found.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class UserRoles {
  private admin = inject(AdminService);

  users = signal<UserRolesRow[]>([]);
  roles = signal<RoleName[]>([]);
  saving = signal<number | null>(null);
  msg = signal(''); isError = signal(false);
  search = '';

  constructor() { this.load(); }

  private ok(m: string) { this.isError.set(false); this.msg.set(m); }
  private fail(e: any) { this.isError.set(true); this.msg.set(errMsg(e)); }

  load() {
    this.admin.roleNames().subscribe({ next: r => this.roles.set(r), error: e => this.fail(e) });
    this.admin.userRoles(this.search).subscribe({ next: u => this.users.set(u), error: e => this.fail(e) });
  }

  has(u: UserRolesRow, id: number) { return u.roleIds.includes(id); }

  toggle(u: UserRolesRow, roleId: number) {
    const next = this.has(u, roleId) ? u.roleIds.filter(x => x !== roleId) : [...u.roleIds, roleId];
    this.saving.set(u.userId);
    this.admin.setUserRoles(u.userId, next).subscribe({
      next: () => {
        this.saving.set(null);
        this.users.update(list => list.map(x => (x.userId === u.userId ? { ...x, roleIds: next } : x)));
        this.ok(`Roles of ${u.fullName} saved.`);
      },
      error: e => { this.saving.set(null); this.fail(e); this.load(); },   // reload so the checkboxes show the real state
    });
  }
}
