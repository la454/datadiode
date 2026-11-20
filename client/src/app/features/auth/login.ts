import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services';
import { errMsg } from '../../core/utils/format';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="dd-login-bg">
      <div class="auth-shell card">
        <section class="auth-hero">
          <div class="hero-brand">
            <img src="logo.png" width="64" height="64" alt="">
            <div>
              <div class="hero-name" lang="ar" dir="rtl">معبر آمن</div>
              <div class="hero-sub">SECURE DATA TRANSFER</div>
            </div>
          </div>
          <div class="hero-slides">
            <img class="slide s1" src="banner.jpg" alt="From internet, secure transfer, air-gapped network">
            <img class="slide s2" src="banner-groups.jpg" alt="Administrator, groups, users and folders">
            <img class="slide s3" src="banner-access.jpg" alt="Shared folder, file types and quota, secure download">
          </div>
          <ul class="hero-points" lang="ar" dir="rtl">
            <li><i class="bi bi-shield-check"></i>نقل آمن للملفات إلى الشبكة المعزولة</li>
            <li><i class="bi bi-people"></i>تحكّم كامل بالمستخدمين والمجموعات</li>
            <li><i class="bi bi-folder-check"></i>مجلد خاص وصلاحيات محددة لكل مستخدم</li>
          </ul>
        </section>

        @if (auth.dashboardMode()) {
          <div class="auth-form">
            <img class="auth-icon" src="logo.png" width="72" height="72" alt="">
            <h4 class="mb-1">Sign in through the dashboard</h4>
            <p class="text-body-secondary mb-4">Your session comes from the company dashboard. Sign in there, then open DataDiode again.</p>
            @if (auth.dashboardUrl()) {
              <a class="btn btn-primary btn-signin w-100 py-2" [href]="auth.dashboardUrl()"><i class="bi bi-box-arrow-up-right me-2"></i>Open the dashboard</a>
            }
            <div class="auth-foot"><i class="bi bi-shield-lock me-1"></i>Encrypted session · Access is logged</div>
          </div>
        } @else {
          <form class="auth-form" (ngSubmit)="submit()">
            <img class="auth-icon" src="logo.png" width="72" height="72" alt="">
            <h4 class="mb-1">Welcome back</h4>
            <p class="text-body-secondary mb-4">Sign in to access your files <span class="mx-1">·</span><span lang="ar" dir="rtl">تسجيل الدخول</span></p>
            <div class="form-floating mb-3">
              <input id="u" class="form-control" name="u" placeholder="Username" [(ngModel)]="username" autocomplete="username" required>
              <label for="u"><i class="bi bi-person me-1"></i>Username or e-mail</label>
            </div>
            <div class="form-floating mb-3">
              <input id="p" class="form-control" name="p" type="password" placeholder="Password" [(ngModel)]="password" autocomplete="current-password" required>
              <label for="p"><i class="bi bi-lock me-1"></i>Password</label>
            </div>
            @if (error()) {
              <div class="alert alert-danger py-2" role="alert"><i class="bi bi-exclamation-triangle me-2"></i>{{ error() }}</div>
            }
            <button class="btn btn-primary btn-signin w-100 py-2" [disabled]="busy()">
              @if (busy()) { <span class="spinner-border spinner-border-sm me-2"></span> }
              Sign in
            </button>
            <div class="auth-foot"><i class="bi bi-shield-lock me-1"></i>Encrypted session · Access is logged</div>
          </form>
        }
      </div>
    </div>
  `,
})
export class Login {
  auth = inject(AuthService);
  private router = inject(Router);
  username = ''; password = '';
  error = signal(''); busy = signal(false);

  submit() {
    this.busy.set(true); this.error.set('');
    this.auth.login(this.username, this.password).subscribe({
      // the permissions decide the first page: refresh them, then "/" picks the home page, the files or "no access"
      next: async () => { await this.auth.refreshAccess(); this.router.navigateByUrl('/'); },
      error: e => { this.busy.set(false); this.error.set(e.status === 401 ? 'Invalid username or password' : errMsg(e)); },
    });
  }
}
