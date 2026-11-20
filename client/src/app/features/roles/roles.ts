import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/services';
import { PermissionItem, RoleRow } from '../../core/models';
import { errMsg } from '../../core/utils/format';

interface RoleForm { roleId: number | null; name: string; nameAr: string; description: string; isSystem: boolean; permissionIds: Set<number>; }

const GROUP_TITLES: Record<string, string> = { Files: 'Files', People: 'People', Security: 'Security' };
const GROUP_ICONS: Record<string, string> = { Files: 'folder2-open', People: 'people', Security: 'shield-lock' };

/** The roles screen: create roles and choose what each role is allowed to do. */
@Component({
  selector: 'app-roles',
  imports: [FormsModule],
  template: `
    <div class="dd-hero">
      <div class="dd-hero-ico"><i class="bi bi-shield-check"></i></div>
      <div class="dd-hero-t">
        <h1>Roles &amp; permissions</h1>
        <p>Create roles and choose what each one may do. A user can have more than one role; the server checks the permission on every request.</p>
      </div>
      <button type="button" class="btn btn-primary dd-hero-act" (click)="open(null)"><i class="bi bi-plus-lg me-1"></i>New role</button>
    </div>

    @if (msg()) {
      <div class="alert alert-dismissible" [class.alert-danger]="isError()" [class.alert-success]="!isError()" role="alert">
        {{ msg() }}<button type="button" class="btn-close" (click)="msg.set('')"></button>
      </div>
    }

    <div class="row g-3">
      @for (r of roles(); track r.roleId) {
        <div class="col-12 col-md-6 col-xxl-4">
          <article class="card h-100 dd-role-card">
            <div class="card-body d-flex flex-column gap-2">
              <div class="d-flex align-items-start justify-content-between gap-2">
                <div class="min-w-0">
                  <h3 class="h5 mb-0 text-truncate">{{ r.name }}</h3>
                  @if (r.nameAr) { <div class="small text-body-secondary" lang="ar" dir="rtl">{{ r.nameAr }}</div> }
                </div>
                @if (r.isSystem) { <span class="badge text-bg-primary">System</span> }
              </div>
              <div class="small text-body-secondary"><i class="bi bi-people me-1"></i>{{ r.userCount }} {{ r.userCount === 1 ? 'user' : 'users' }}</div>
              @if (r.description) { <p class="small text-body-secondary mb-0">{{ r.description }}</p> }
              <div class="d-flex flex-wrap gap-1 my-1">
                @for (id of r.permissionIds.slice(0, 6); track id) { <span class="badge text-bg-light border fw-normal">{{ permName(id) }}</span> }
                @if (r.permissionIds.length > 6) { <span class="badge text-bg-light border fw-normal">+{{ r.permissionIds.length - 6 }}</span> }
                @if (!r.permissionIds.length) { <span class="small text-body-secondary">No permissions</span> }
              </div>
              <div class="d-flex gap-2 mt-auto pt-2">
                <button type="button" class="btn btn-sm btn-outline-primary" (click)="open(r)"><i class="bi bi-pencil me-1"></i>Edit</button>
                @if (!r.isSystem) {
                  <button type="button" class="btn btn-sm btn-outline-danger" (click)="remove(r)"><i class="bi bi-trash3 me-1"></i>Delete</button>
                }
              </div>
            </div>
          </article>
        </div>
      }
    </div>

    @if (form(); as f) {
      <div class="dd-backdrop" (click)="close()">
        <div class="dd-modal" (click)="$event.stopPropagation()" role="dialog" aria-modal="true" aria-label="Role">
          <div class="dd-modal-head">
            <div><h2 class="h5 mb-0">{{ f.roleId ? 'Edit role' : 'New role' }}</h2><div class="small text-body-secondary">Choose the permissions this role has.</div></div>
            <button type="button" class="btn-close" (click)="close()" aria-label="Close"></button>
          </div>
          <div class="dd-modal-body">
            <div class="row g-3 mb-3">
              <div class="col-12 col-md-6">
                <label class="form-label small mb-1">Role name <span class="text-danger">*</span></label>
                <input class="form-control" name="rname" [ngModel]="f.name" (ngModelChange)="patch('name', $event)" [disabled]="f.isSystem" maxlength="80">
              </div>
              <div class="col-12 col-md-6">
                <label class="form-label small mb-1">Arabic name</label>
                <input class="form-control" name="rnamear" dir="rtl" [ngModel]="f.nameAr" (ngModelChange)="patch('nameAr', $event)" maxlength="100">
              </div>
              <div class="col-12">
                <label class="form-label small mb-1">Description</label>
                <input class="form-control" name="rdesc" [ngModel]="f.description" (ngModelChange)="patch('description', $event)" maxlength="300">
              </div>
            </div>
            <div class="row g-3">
              @for (g of groups(); track g.key) {
                <div class="col-12 col-md-6 col-xl-4">
                  <section class="dd-pgroup h-100">
                    <button type="button" class="dd-pgroup-head" (click)="toggleGroup(g.items)">
                      <span><i class="bi bi-{{ g.icon }} me-2"></i>{{ g.title }}</span>
                      <span class="badge" [class.text-bg-primary]="groupAll(g.items)" [class.text-bg-light]="!groupAll(g.items)">{{ groupAll(g.items) ? 'Clear all' : 'Select all' }}</span>
                    </button>
                    @for (p of g.items; track p.permissionId) {
                      <label class="dd-prow">
                        <input class="form-check-input mt-0" type="checkbox" [checked]="has(p.permissionId)" (change)="toggle(p.permissionId)">
                        <span><span class="d-block">{{ p.nameEn }}</span><small class="text-body-secondary">{{ p.code }}</small></span>
                      </label>
                    }
                  </section>
                </div>
              }
            </div>
            @if (f.name === 'Admin') {
              <div class="alert alert-info small mt-3 mb-0"><i class="bi bi-info-circle me-1"></i>The Admin role always keeps "Manage roles" and "Assign roles to users", so nobody can lock the system.</div>
            }
          </div>
          <div class="dd-modal-foot">
            <button type="button" class="btn btn-outline-secondary" (click)="close()">Cancel</button>
            <button type="button" class="btn btn-primary" [disabled]="busy() || f.name.trim().length < 2" (click)="save()">
              @if (busy()) { <span class="spinner-border spinner-border-sm me-2"></span> }Save role</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class Roles {
  private admin = inject(AdminService);

  roles = signal<RoleRow[]>([]);
  permissions = signal<PermissionItem[]>([]);
  form = signal<RoleForm | null>(null);
  busy = signal(false);
  msg = signal(''); isError = signal(false);
  groups = computed(() => {
    const by = new Map<string, PermissionItem[]>();
    for (const p of this.permissions()) { const l = by.get(p.group) ?? []; l.push(p); by.set(p.group, l); }
    return [...by.entries()].map(([key, items]) => ({ key, title: GROUP_TITLES[key] ?? key, icon: GROUP_ICONS[key] ?? 'key', items }));
  });

  constructor() { this.load(); }

  private ok(m: string) { this.isError.set(false); this.msg.set(m); }
  private fail(e: any) { this.isError.set(true); this.msg.set(errMsg(e)); }

  load() {
    this.admin.permissions().subscribe({ next: p => this.permissions.set(p), error: e => this.fail(e) });
    this.admin.roles().subscribe({ next: r => this.roles.set(r), error: e => this.fail(e) });
  }

  open(r: RoleRow | null) {
    this.form.set({
      roleId: r?.roleId ?? null, name: r?.name ?? '', nameAr: r?.nameAr ?? '', description: r?.description ?? '',
      isSystem: !!r?.isSystem, permissionIds: new Set(r?.permissionIds ?? []),
    });
  }
  close() { this.form.set(null); }

  has(id: number) { return !!this.form()?.permissionIds.has(id); }
  toggle(id: number) {
    const f = this.form();
    if (!f) return;
    const next = new Set(f.permissionIds);
    if (!next.delete(id)) next.add(id);
    this.form.set({ ...f, permissionIds: next });
  }
  toggleGroup(items: PermissionItem[]) {
    const f = this.form();
    if (!f) return;
    const all = items.every(p => f.permissionIds.has(p.permissionId));
    const next = new Set(f.permissionIds);
    for (const p of items) { if (all) next.delete(p.permissionId); else next.add(p.permissionId); }
    this.form.set({ ...f, permissionIds: next });
  }
  groupAll(items: PermissionItem[]) { const f = this.form(); return !!f && items.every(p => f.permissionIds.has(p.permissionId)); }
  patch(k: 'name' | 'nameAr' | 'description', v: string) { const f = this.form(); if (f) this.form.set({ ...f, [k]: v }); }

  save() {
    const f = this.form();
    if (!f) return;
    this.busy.set(true);
    const body = { name: f.name, nameAr: f.nameAr, description: f.description, permissionIds: [...f.permissionIds] };
    const call = f.roleId ? this.admin.updateRole(f.roleId, body) : this.admin.createRole(body);
    call.subscribe({
      next: () => { this.busy.set(false); this.ok('Role saved.'); this.form.set(null); this.load(); },
      error: e => { this.busy.set(false); this.fail(e); },
    });
  }

  remove(r: RoleRow) {
    if (!confirm(`Delete the role "${r.name}"? This cannot be undone.`)) return;
    this.admin.deleteRole(r.roleId).subscribe({ next: () => { this.ok('Role deleted.'); this.load(); }, error: e => this.fail(e) });
  }

  permName(id: number) { return this.permissions().find(p => p.permissionId === id)?.nameEn ?? ''; }
}
