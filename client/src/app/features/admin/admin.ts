import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminService, AuthService } from '../../core/services';
import { Group, User } from '../../core/models';
import { errMsg, formatSize } from '../../core/utils/format';

interface Row extends User { extText: string; quota: number; folder: string; group: number | null; active: boolean; mail: string; name: string; }

const COLORS = ['blue', 'green', 'orange', 'purple', 'pink', 'teal', 'amber', 'red'];

@Component({
  selector: 'app-admin',
  imports: [FormsModule],
  template: `
    <div class="dd-hero">
      <div class="dd-hero-ico"><i class="bi bi-people"></i></div>
      <div class="dd-hero-t">
        <h1>Users &amp; groups</h1>
        <p>Organise users into groups, assign folders and control file types and quotas. Roles are assigned in “Users &amp; roles”.</p>
      </div>
      <button type="button" class="btn btn-primary dd-hero-act" (click)="showAdd.set(!showAdd())">
        <i class="bi me-1" [class.bi-plus-lg]="!showAdd()" [class.bi-x-lg]="showAdd()"></i>{{ showAdd() ? 'Close' : 'Add user' }}
      </button>
    </div>

    @if (msg()) {
      <div class="alert alert-dismissible" [class.alert-danger]="isError()" [class.alert-success]="!isError()" role="alert">
        {{ msg() }}<button type="button" class="btn-close" (click)="msg.set('')"></button>
      </div>
    }

    <div class="row g-3 mb-4">
      <div class="col-6 col-lg-3">
        <div class="card stat"><div class="card-body">
          <div class="stat-icon ft-blue"><i class="bi bi-people"></i></div>
          <div class="stat-value">{{ rows().length }}</div><div class="stat-label">Users</div>
        </div></div>
      </div>
      <div class="col-6 col-lg-3">
        <div class="card stat"><div class="card-body">
          <div class="stat-icon ft-purple"><i class="bi bi-diagram-3"></i></div>
          <div class="stat-value">{{ groups().length }}</div><div class="stat-label">Groups</div>
        </div></div>
      </div>
      <div class="col-6 col-lg-3">
        <div class="card stat"><div class="card-body">
          <div class="stat-icon ft-orange"><i class="bi bi-shield-check"></i></div>
          <div class="stat-value">{{ adminCount() }}</div><div class="stat-label">Administrators</div>
        </div></div>
      </div>
      <div class="col-6 col-lg-3">
        <div class="card stat"><div class="card-body">
          <div class="stat-icon ft-gray"><i class="bi bi-person-dash"></i></div>
          <div class="stat-value">{{ countFor(0) }}</div><div class="stat-label">Without a group</div>
        </div></div>
      </div>
    </div>

    @if (showAdd()) {
      <div class="card mb-4">
        <div class="card-header py-3"><i class="bi bi-person-plus me-2"></i>New user</div>
        <div class="card-body">
          <form class="row g-3" (ngSubmit)="create()">
            <div class="col-12 col-md-6 col-xl-3">
              <label class="form-label">Username</label>
              <input class="form-control" name="n" [(ngModel)]="nu.username" required autocomplete="off">
            </div>
            <div class="col-12 col-md-6 col-xl-3">
              <label class="form-label">Password</label>
              <input class="form-control" name="p" type="password" [(ngModel)]="nu.password" minlength="6" required autocomplete="new-password">
            </div>
            <div class="col-12 col-md-6 col-xl-3">
              <label class="form-label">Full name</label>
              <input class="form-control" name="fn" [(ngModel)]="nu.fullName" placeholder="optional">
            </div>
            <div class="col-12 col-md-6 col-xl-3">
              <label class="form-label">E-mail</label>
              <input class="form-control" name="em" type="email" [(ngModel)]="nu.email" placeholder="optional (links a dashboard user)">
            </div>
            <div class="col-12">
              <label class="form-label">Folder path</label>
              <input class="form-control" name="f" [(ngModel)]="nu.folderPath" placeholder="D:\\Incoming\\alice or \\\\server\\share\\My Folder (blank = auto-created)">
            </div>
            <div class="col-12 col-md-6 col-xl-4">
              <label class="form-label">Allowed file types</label>
              <input class="form-control" name="e" [(ngModel)]="nu.allowedExtensions" placeholder="pdf, png, docx (blank = all)">
            </div>
            <div class="col-6 col-md-3 col-xl-2">
              <label class="form-label">Quota (MB)</label>
              <input class="form-control" name="q" type="number" min="0" [(ngModel)]="nu.quotaMb">
            </div>
            <div class="col-6 col-md-3 col-xl-3">
              <label class="form-label">Group</label>
              <select class="form-select" name="g" [(ngModel)]="nu.groupId">
                <option [ngValue]="null">No group</option>
                @for (g of groups(); track g.id) { <option [ngValue]="g.id">{{ g.name }}</option> }
              </select>
            </div>
            <div class="col-12 col-xl-3 d-flex align-items-end">
              <div class="form-check form-switch mb-2">
                <input class="form-check-input" id="adm" name="a" type="checkbox" [(ngModel)]="nu.isAdmin">
                <label class="form-check-label" for="adm">Administrator <span class="text-body-secondary small">(Admin role, else User)</span></label>
              </div>
            </div>
            <div class="col-12 d-flex justify-content-end">
              <button class="btn btn-primary"><i class="bi bi-plus-lg me-1"></i>Create user</button>
            </div>
          </form>
        </div>
      </div>
    }

    <div class="row g-4">
      <!-- Groups -->
      <div class="col-12 col-lg-4 col-xl-3">
        <div class="card">
          <div class="card-header py-3"><i class="bi bi-diagram-3 me-2"></i>Groups</div>
          <div class="list-group list-group-flush group-list">
            <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
                    [class.active]="filterGroup() === null" (click)="filterGroup.set(null)">
              <span><i class="bi bi-people me-2"></i>All users</span><span class="badge rounded-pill text-bg-light">{{ rows().length }}</span>
            </button>
            <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
                    [class.active]="filterGroup() === 0" (click)="filterGroup.set(0)">
              <span><i class="bi bi-person-dash me-2"></i>No group</span><span class="badge rounded-pill text-bg-light">{{ countFor(0) }}</span>
            </button>
            @for (g of groups(); track g.id) {
              <div class="list-group-item list-group-item-action d-flex align-items-center gap-2 group-item"
                   [class.active]="filterGroup() === g.id" (click)="filterGroup.set(g.id)" role="button">
                <span class="dot" [class]="'ft-' + color(g.name)"></span>
                @if (editingGroup() === g.id) {
                  <input class="form-control form-control-sm" [(ngModel)]="groupName" (click)="$event.stopPropagation()"
                         (keydown.enter)="renameGroup(g)" (keydown.escape)="editingGroup.set(null)">
                  <button class="icon-btn c-green" (click)="$event.stopPropagation(); renameGroup(g)" title="Save"><i class="bi bi-check-lg"></i></button>
                } @else {
                  <span class="flex-grow-1 text-truncate">{{ g.name }}</span>
                  <span class="badge rounded-pill text-bg-light">{{ countFor(g.id) }}</span>
                  <span class="group-actions d-flex gap-1">
                    <button class="icon-btn" (click)="$event.stopPropagation(); startRename(g)" title="Rename"><i class="bi bi-pencil"></i></button>
                    <button class="icon-btn danger" (click)="$event.stopPropagation(); deleteGroup(g)" title="Delete group"><i class="bi bi-trash"></i></button>
                  </span>
                }
              </div>
            }
          </div>
          <div class="card-body border-top">
            <form class="input-group input-group-sm" (ngSubmit)="addGroup()">
              <input class="form-control" name="gn" [(ngModel)]="newGroup" placeholder="New group name" maxlength="100">
              <button class="btn btn-primary" [disabled]="!newGroup.trim()"><i class="bi bi-plus-lg"></i></button>
            </form>
          </div>
        </div>
      </div>

      <!-- Users -->
      <div class="col-12 col-lg-8 col-xl-9">
        <div class="d-flex flex-wrap gap-2 align-items-center mb-3">
          <div class="input-group search-wide">
            <span class="input-group-text bg-body border-end-0"><i class="bi bi-search"></i></span>
            <input class="form-control border-start-0" [ngModel]="query()" (ngModelChange)="query.set($event)"
                   placeholder="Search users, folders or groups">
            @if (query()) { <button class="btn btn-outline-secondary" (click)="query.set('')" title="Clear"><i class="bi bi-x-lg"></i></button> }
          </div>
          <span class="text-body-secondary small ms-auto">
            {{ filtered().length }} of {{ rows().length }} users
            @if (filterGroup() !== null) { · <a href="javascript:void(0)" (click)="filterGroup.set(null)">show all</a> }
          </span>
        </div>

        <div class="row g-3">
          @for (r of filtered(); track r.id) {
            <div class="col-12 col-xxl-6">
              <div class="card h-100 user-card">
                <div class="card-body">
                  <div class="d-flex align-items-center justify-content-between">
                    <div class="d-flex align-items-center min-w-0">
                      <span class="avatar me-3" [class]="'avatar me-3 av-' + (r.group ? color(groupName_(r.group)) : 'gray')">{{ r.username.charAt(0).toUpperCase() }}</span>
                      <div class="min-w-0">
                        <div class="fw-semibold text-truncate">{{ r.fullName || r.username }}
                          @if (!r.isActive) { <span class="badge text-bg-secondary ms-1">disabled</span> }
                          @if (r.fromDashboard) { <span class="badge text-bg-info ms-1">dashboard</span> }
                        </div>
                        <div class="small text-body-secondary text-truncate">{{ r.username }}@if (r.email && r.email !== r.username) { · {{ r.email }} }</div>
                        <div class="d-flex flex-wrap gap-1 mt-1">
                          @for (n of r.roles; track n) { <span class="badge" [class.text-bg-primary]="n === 'Admin'" [class.text-bg-light]="n !== 'Admin'" [class.border]="n !== 'Admin'">{{ n }}</span> }
                          @if (!r.roles.length) { <span class="badge text-bg-warning">no role</span> }
                          @if (r.groupId) {
                            <span class="badge-soft" [class]="'badge-soft ft-' + color(groupName_(r.groupId))">{{ groupName_(r.groupId) }}</span>
                          } @else { <span class="small text-body-secondary">No group</span> }
                        </div>
                      </div>
                    </div>
                    <div class="d-flex gap-1">
                      <button class="icon-btn c-blue" (click)="toggle(r.id)" [title]="open().has(r.id) ? 'Close' : 'Edit'">
                        <i class="bi" [class.bi-pencil]="!open().has(r.id)" [class.bi-chevron-up]="open().has(r.id)"></i></button>
                      <button class="icon-btn c-orange" (click)="reset(r)" title="Reset password"><i class="bi bi-key"></i></button>
                      <button class="icon-btn c-red" (click)="del(r)" [disabled]="r.username === auth.user()?.username" title="Delete user"><i class="bi bi-trash"></i></button>
                    </div>
                  </div>

                  <div class="mt-3">
                    <div class="d-flex justify-content-between small text-body-secondary mb-1">
                      <span>{{ fmt(r.usedBytes) }} used</span>
                      <span>{{ r.quotaMb ? r.quotaMb + ' MB quota' : 'No quota' }}</span>
                    </div>
                    <div class="progress" style="height:6px">
                      <div class="progress-bar" [class.bg-danger]="pct(r) > 90" [class.bg-warning]="pct(r) > 70 && pct(r) <= 90"
                           [style.width.%]="pct(r)"></div>
                    </div>
                  </div>
                  <div class="small text-body-secondary mt-2 text-truncate" [title]="r.folderPath"><i class="bi bi-folder2 me-1"></i>{{ r.folderPath }}</div>

                  @if (open().has(r.id)) {
                    <hr>
                    <div class="row g-2">
                      <div class="col-12 col-sm-6">
                        <label class="form-label small mb-1">Full name</label>
                        <input class="form-control form-control-sm" [(ngModel)]="r.name">
                      </div>
                      <div class="col-12 col-sm-6">
                        <label class="form-label small mb-1">E-mail</label>
                        <input class="form-control form-control-sm" type="email" [(ngModel)]="r.mail">
                      </div>
                      <div class="col-12">
                        <label class="form-label small mb-1">Folder path</label>
                        <input class="form-control form-control-sm" [(ngModel)]="r.folder">
                      </div>
                      <div class="col-12 col-sm-6">
                        <label class="form-label small mb-1">Group</label>
                        <select class="form-select form-select-sm" [(ngModel)]="r.group">
                          <option [ngValue]="null">No group</option>
                          @for (g of groups(); track g.id) { <option [ngValue]="g.id">{{ g.name }}</option> }
                        </select>
                      </div>
                      <div class="col-6 col-sm-3">
                        <label class="form-label small mb-1">Quota (MB)</label>
                        <input class="form-control form-control-sm" type="number" min="0" [(ngModel)]="r.quota">
                      </div>
                      <div class="col-12 col-sm-8">
                        <label class="form-label small mb-1">Allowed file types</label>
                        <input class="form-control form-control-sm" [(ngModel)]="r.extText" placeholder="all types">
                      </div>
                      <div class="col-12 col-sm-4 d-flex align-items-end">
                        <div class="form-check form-switch mb-1">
                          <input class="form-check-input" type="checkbox" [id]="'act' + r.id" [(ngModel)]="r.active" [disabled]="r.username === auth.user()?.username">
                          <label class="form-check-label small" [for]="'act' + r.id">Active</label>
                        </div>
                      </div>
                    </div>
                    <div class="d-flex gap-2 mt-3">
                      <button class="btn btn-sm btn-primary" (click)="save(r)"><i class="bi bi-check-lg me-1"></i>Save changes</button>
                      <button class="btn btn-sm btn-outline-secondary" (click)="toggle(r.id)">Cancel</button>
                    </div>
                  } @else if (r.allowedExtensions.length) {
                    <div class="mt-2 d-flex flex-wrap gap-1">
                      @for (e of r.allowedExtensions; track e) { <span class="badge text-bg-light border">.{{ e }}</span> }
                    </div>
                  }
                </div>
              </div>
            </div>
          } @empty {
            <div class="col-12">
              <div class="card"><div class="card-body text-center text-body-secondary py-5">
                <i class="bi bi-search fs-1 d-block mb-2"></i>No users match your search.
              </div></div>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class Admin {
  auth = inject(AuthService);
  private admin = inject(AdminService);
  rows = signal<Row[]>([]);
  groups = signal<Group[]>([]);
  msg = signal(''); isError = signal(false);
  showAdd = signal(false);
  query = signal('');
  filterGroup = signal<number | null>(null); // null = all, 0 = no group, else group id
  open = signal<Set<number>>(new Set());
  editingGroup = signal<number | null>(null);
  groupName = ''; newGroup = '';
  fmt = formatSize;
  nu = this.blank();

  adminCount = computed(() => this.rows().filter(r => r.isAdmin).length);
  filtered = computed(() => {
    const q = this.query().trim().toLowerCase(), f = this.filterGroup();
    return this.rows().filter(r =>
      (f === null || (f === 0 ? !r.groupId : r.groupId === f)) &&
      (!q || r.username.toLowerCase().includes(q) || r.folderPath.toLowerCase().includes(q) ||
        (r.fullName ?? '').toLowerCase().includes(q) || (r.email ?? '').toLowerCase().includes(q) ||
        this.groupName_(r.groupId).toLowerCase().includes(q)));
  });

  constructor() { this.loadGroups(); this.load(); }

  private blank() { return { username: '', password: '', isAdmin: false, allowedExtensions: '', quotaMb: 100, folderPath: '', groupId: null as number | null, email: '', fullName: '' }; }
  private toRow = (u: User): Row => ({
    ...u, extText: u.allowedExtensions.join(', '), quota: u.quotaMb, folder: u.folderPath, group: u.groupId,
    active: u.isActive, mail: u.email ?? '', name: u.fullName ?? '',
  });
  private ok(m: string) { this.isError.set(false); this.msg.set(m); }
  private fail(e: any) { this.isError.set(true); this.msg.set(errMsg(e)); }

  groupName_(id: number | null) { return this.groups().find(g => g.id === id)?.name ?? ''; }
  color(name: string) { let h = 0; for (const c of name) h = (h * 31 + c.charCodeAt(0)) >>> 0; return COLORS[h % COLORS.length]; }
  countFor(id: number) { return id === 0 ? this.rows().filter(r => !r.groupId).length : this.rows().filter(r => r.groupId === id).length; }
  pct(r: Row) { return r.quotaMb ? Math.min(100, (r.usedBytes / (r.quotaMb * 1024 * 1024)) * 100) : 0; }
  toggle(id: number) { this.open.update(s => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; }); }

  load() { this.admin.users().subscribe(us => this.rows.set(us.map(this.toRow))); }
  loadGroups() { this.admin.groups().subscribe(gs => this.groups.set(gs)); }

  create() {
    this.admin.createUser(this.nu).subscribe({
      next: u => { this.ok(`User "${u.username}" created.`); this.nu = this.blank(); this.showAdd.set(false); this.load(); },
      error: e => this.fail(e),
    });
  }
  save(r: Row) {
    this.admin.saveSettings(r.id, {
      allowedExtensions: r.extText, quotaMb: r.quota, folderPath: r.folder, groupId: r.group,
      isActive: r.active, email: r.mail, fullName: r.name,
    }).subscribe({
      next: u => { this.ok(`Saved settings for ${u.username}.`); this.toggle(r.id); this.load(); },
      error: e => this.fail(e),
    });
  }
  reset(r: Row) {
    const p = prompt(`New password for ${r.username} (min 6 chars):`);
    if (!p) return;
    this.admin.resetPassword(r.id, p).subscribe({ next: () => this.ok('Password reset.'), error: e => this.fail(e) });
  }
  del(r: Row) {
    if (!confirm(`Delete user "${r.username}" and ALL of their files?`)) return;
    this.admin.deleteUser(r.id).subscribe({ next: () => { this.ok('User deleted.'); this.load(); }, error: e => this.fail(e) });
  }

  addGroup() {
    const n = this.newGroup.trim();
    if (!n) return;
    this.admin.createGroup(n).subscribe({
      next: g => { this.ok(`Group "${g.name}" created.`); this.newGroup = ''; this.loadGroups(); },
      error: e => this.fail(e),
    });
  }
  startRename(g: Group) { this.groupName = g.name; this.editingGroup.set(g.id); }
  renameGroup(g: Group) {
    this.admin.renameGroup(g.id, this.groupName).subscribe({
      next: () => { this.editingGroup.set(null); this.ok('Group renamed.'); this.loadGroups(); },
      error: e => this.fail(e),
    });
  }
  deleteGroup(g: Group) {
    if (!confirm(`Delete group "${g.name}"? Its ${this.countFor(g.id)} member(s) will stay but become ungrouped.`)) return;
    this.admin.deleteGroup(g.id).subscribe({
      next: () => { if (this.filterGroup() === g.id) this.filterGroup.set(null); this.ok('Group deleted.'); this.loadGroups(); this.load(); },
      error: e => this.fail(e),
    });
  }
}
