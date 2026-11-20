import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, UpperCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, FilesService } from '../../core/services';
import { FileItem } from '../../core/models';
import { errMsg, formatSize } from '../../core/utils/format';
import { FileIcon } from './file-icon';

@Component({
  selector: 'app-files',
  imports: [DatePipe, UpperCasePipe, FormsModule, FileIcon],
  template: `
    <div class="dd-hero">
      <div class="dd-hero-ico"><i class="bi bi-folder2-open"></i></div>
      <div class="dd-hero-t">
        <h1>My files</h1>
        <p>Files available to you in your assigned folder. Download them securely; everything is logged.</p>
      </div>
      <button type="button" class="btn btn-outline-light dd-hero-act" (click)="refresh()" [disabled]="loading()" title="Refresh">
        <i class="bi bi-arrow-clockwise me-1" [class.spin]="loading()"></i>Refresh</button>
    </div>

    @if (auth.user(); as u) {
      <div class="row g-3 mb-4">
        <div class="col-6 col-lg-3">
          <div class="card stat h-100"><div class="card-body">
            <div class="stat-icon bg-primary-subtle text-primary"><i class="bi bi-files"></i></div>
            <div class="stat-value">{{ files().length }}</div>
            <div class="stat-label">Files</div>
          </div></div>
        </div>
        <div class="col-6 col-lg-3">
          <div class="card stat h-100"><div class="card-body">
            <div class="stat-icon bg-success-subtle text-success"><i class="bi bi-hdd"></i></div>
            <div class="stat-value">{{ fmt(u.usedBytes) }}</div>
            <div class="stat-label">Used</div>
          </div></div>
        </div>
        <div class="col-12 col-lg-6">
          <div class="card stat h-100"><div class="card-body">
            <div class="d-flex justify-content-between small mb-2">
              <span class="text-body-secondary">Quota</span>
              <span><strong>{{ fmt(u.usedBytes) }}</strong> of {{ u.quotaMb }} MB ({{ percent().toFixed(0) }}%)</span>
            </div>
            <div class="progress mb-3" style="height: 10px">
              <div class="progress-bar" [class.bg-warning]="percent() >= 75" [class.bg-danger]="percent() >= 90"
                   [style.width.%]="percent()"></div>
            </div>
            <div class="small text-body-secondary">
              Allowed types:
              @if (u.allowedExtensions.length) {
                @for (e of u.allowedExtensions; track e) { <span class="badge text-bg-light border me-1">.{{ e }}</span> }
              } @else { <span class="badge text-bg-light border">all</span> }
            </div>
          </div></div>
        </div>
      </div>
    }

    @if (msg()) {
      <div class="alert alert-danger alert-dismissible" role="alert">
        {{ msg() }}<button type="button" class="btn-close" (click)="msg.set('')"></button>
      </div>
    }

    <div class="card">
      <div class="card-header py-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
        <span><i class="bi bi-folder2-open me-2"></i>Files</span>
        <div class="input-group input-group-sm search">
          <span class="input-group-text"><i class="bi bi-search"></i></span>
          <input class="form-control" placeholder="Search files…" [ngModel]="q()" (ngModelChange)="q.set($event)">
        </div>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th>Name</th>
              <th class="d-none d-sm-table-cell">Type</th>
              <th class="d-none d-sm-table-cell">Size</th>
              <th class="d-none d-md-table-cell">Created</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (f of shown(); track f.name) {
              <tr>
                <td class="name-cell">
                  <div class="d-flex align-items-center">
                    <span class="d-sm-none me-3 flex-shrink-0" [title]="f.extension ? '.' + f.extension : 'file'">
                      <app-file-icon [ext]="f.extension" />
                    </span>
                    <div class="text-truncate">
                      <div class="text-truncate fw-medium">{{ f.name }}</div>
                      <div class="small text-body-secondary d-sm-none">{{ f.extension | uppercase }} · {{ fmt(f.size) }} · {{ f.created | date:'short' }}</div>
                    </div>
                  </div>
                </td>
                <td class="d-none d-sm-table-cell text-nowrap">
                  <span [title]="f.extension ? '.' + f.extension : 'file'">
                    <app-file-icon [ext]="f.extension" />
                  </span>
                </td>
                <td class="d-none d-sm-table-cell text-nowrap">{{ fmt(f.size) }}</td>
                <td class="d-none d-md-table-cell text-nowrap">{{ f.created | date:'medium' }}</td>
                <td class="text-end text-nowrap">
                  @if (auth.can('files.download')) {
                    <button class="icon-btn c-blue" (click)="download(f)" title="Download" aria-label="Download">
                      <i class="bi bi-download"></i></button>
                  }
                  @if (auth.can('files.delete')) {
                    <button class="icon-btn c-red ms-1" (click)="remove(f)" title="Delete" aria-label="Delete">
                      <i class="bi bi-trash3"></i></button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="text-center text-body-secondary py-5">
                <i class="bi bi-inbox fs-1 d-block mb-2"></i>
                {{ q() ? 'No files match your search.' : 'No files in your folder yet.' }}
              </td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class Files {
  auth = inject(AuthService);
  private filesApi = inject(FilesService);
  files = signal<FileItem[]>([]);
  q = signal('');
  msg = signal('');
  loading = signal(false);
  fmt = formatSize;
  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    const list = q ? this.files().filter(f => f.name.toLowerCase().includes(q)) : this.files();
    return [...list].sort((a, b) => b.created.localeCompare(a.created)); // newest created first
  });
  percent = computed(() => {
    const u = this.auth.user();
    return u && u.quotaMb ? Math.min(100, (u.usedBytes / (u.quotaMb * 1048576)) * 100) : 0;
  });

  constructor() { this.refresh(); }

  refresh() {
    this.loading.set(true);
    this.filesApi.list().subscribe({
      next: f => { this.files.set(f); this.loading.set(false); },
      error: e => { this.msg.set(errMsg(e)); this.loading.set(false); },
    });
    this.auth.loadMe().subscribe({ error: () => { /* a 401 is handled by the interceptor */ } });
  }

  download(f: FileItem) {
    this.filesApi.download(f.name).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = f.name; a.click();
        URL.revokeObjectURL(url);
      },
      error: e => this.msg.set(errMsg(e)),
    });
  }

  remove(f: FileItem) {
    if (!confirm(`Delete "${f.name}"? This cannot be undone.`)) return;
    this.filesApi.remove(f.name).subscribe({
      next: () => { this.msg.set(''); this.refresh(); },
      error: e => this.msg.set(errMsg(e)),
    });
  }
}
