import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/services';
import { AuditRow } from '../../core/models';
import { errMsg } from '../../core/utils/format';

/** The audit log: sign-ins, refused calls, and every change of users, roles and files. */
@Component({
  selector: 'app-audit',
  imports: [DatePipe, FormsModule],
  template: `
    <div class="dd-hero">
      <div class="dd-hero-ico"><i class="bi bi-journal-text"></i></div>
      <div class="dd-hero-t">
        <h1>Audit log</h1>
        <p>Who signed in, what was refused, and every change of users, groups, roles and files. Newest first.</p>
      </div>
      <button type="button" class="btn btn-outline-light dd-hero-act" (click)="load()" [disabled]="loading()">
        <i class="bi bi-arrow-clockwise me-1" [class.spin]="loading()"></i>Refresh</button>
    </div>

    @if (msg()) {
      <div class="alert alert-danger alert-dismissible" role="alert">{{ msg() }}<button type="button" class="btn-close" (click)="msg.set('')"></button></div>
    }

    <div class="card">
      <div class="card-header py-3">
        <div class="row g-2 align-items-center">
          <div class="col-12 col-md-4">
            <div class="input-group">
              <span class="input-group-text bg-body"><i class="bi bi-search"></i></span>
              <input class="form-control" name="q" placeholder="Search user, details, IP…" [(ngModel)]="search" (keyup.enter)="load()">
            </div>
          </div>
          <div class="col-6 col-md-3">
            <select class="form-select" name="a" [(ngModel)]="action" (ngModelChange)="load()">
              <option value="">All actions</option>
              @for (a of actions(); track a) { <option [value]="a">{{ a }}</option> }
            </select>
          </div>
          <div class="col-6 col-md-2">
            <select class="form-select" name="s" [(ngModel)]="result" (ngModelChange)="load()">
              <option value="">Any result</option><option value="ok">Succeeded</option><option value="fail">Refused / failed</option>
            </select>
          </div>
          <div class="col-6 col-md-2">
            <select class="form-select" name="t" [(ngModel)]="take" (ngModelChange)="load()">
              <option [ngValue]="100">Last 100</option><option [ngValue]="200">Last 200</option><option [ngValue]="500">Last 500</option><option [ngValue]="1000">Last 1000</option>
            </select>
          </div>
          <div class="col-6 col-md-1 text-md-end"><button type="button" class="btn btn-primary w-100" (click)="load()">Go</button></div>
        </div>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0 dd-audit">
          <thead class="table-light">
            <tr><th>When</th><th>User</th><th>Action</th><th class="d-none d-lg-table-cell">Details</th><th class="d-none d-md-table-cell">IP</th><th>Result</th></tr>
          </thead>
          <tbody>
            @for (r of rows(); track r.id) {
              <tr>
                <td class="text-nowrap small">{{ r.createdAt | date:'medium' }}</td>
                <td>{{ r.username || '—' }}</td>
                <td><span class="badge dd-action" [class]="'badge dd-action ' + tone(r)">{{ r.action }}</span></td>
                <td class="d-none d-lg-table-cell small">{{ r.details }}@if (r.errorMessage) { <span class="text-danger"> · {{ r.errorMessage }}</span> }</td>
                <td class="d-none d-md-table-cell small text-body-secondary text-nowrap">{{ r.ipAddress }}</td>
                <td>@if (r.success) { <i class="bi bi-check-circle-fill text-success" title="Succeeded"></i> } @else { <i class="bi bi-x-circle-fill text-danger" title="Refused / failed"></i> }</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="text-center text-body-secondary py-5"><i class="bi bi-journal-x fs-1 d-block mb-2"></i>{{ loading() ? 'Loading…' : 'No entries match.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class Audit {
  private admin = inject(AdminService);

  rows = signal<AuditRow[]>([]);
  actions = signal<string[]>([]);
  loading = signal(false);
  msg = signal('');
  search = ''; action = ''; result = ''; take = 200;

  constructor() {
    this.admin.auditActions().subscribe({ next: a => this.actions.set(a), error: () => { /* the filter just stays empty */ } });
    this.load();
  }

  load() {
    this.loading.set(true);
    const success = this.result === 'ok' ? true : this.result === 'fail' ? false : null;
    this.admin.audit({ action: this.action, search: this.search, success, take: this.take }).subscribe({
      next: r => { this.rows.set(r); this.loading.set(false); this.msg.set(''); },
      error: e => { this.loading.set(false); this.msg.set(errMsg(e)); },
    });
  }

  tone(r: AuditRow) {
    if (!r.success) return 'bg-danger-subtle text-danger-emphasis';
    if (/Deleted|Reset/.test(r.action)) return 'bg-warning-subtle text-warning-emphasis';
    if (/Created|Assigned|Login$/.test(r.action)) return 'bg-success-subtle text-success-emphasis';
    return 'bg-primary-subtle text-primary-emphasis';
  }
}
