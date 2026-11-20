import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services';

/** A dashboard user that has no role here yet: an administrator has to assign one first. */
@Component({
  selector: 'app-no-access',
  template: `
    <div class="dd-login-bg">
      <div class="dd-noaccess card">
        <div class="dd-noaccess-ico"><i class="bi bi-lock"></i></div>
        <h2 class="h4 mb-2">No access yet</h2>
        <p class="text-body-secondary mb-3">
          You signed in successfully, but no role has been assigned to your account in DataDiode yet.
          Ask an administrator to give you a role, then try again.
        </p>
        <p class="small text-body-secondary mb-4">
          <i class="bi bi-person-circle me-1"></i>{{ auth.displayName() }}@if (auth.user()?.email; as mail) { — {{ mail }} }
        </p>
        <div class="d-flex flex-wrap gap-2 justify-content-center">
          <button type="button" class="btn btn-primary" (click)="retry()"><i class="bi bi-arrow-clockwise me-1"></i>Try again</button>
          <button type="button" class="btn btn-outline-secondary" (click)="auth.logout()">
            <i class="bi bi-box-arrow-left me-1"></i>{{ auth.dashboardMode() ? 'Back to the dashboard' : 'Sign out' }}</button>
        </div>
      </div>
    </div>
  `,
})
export class NoAccess {
  auth = inject(AuthService);
  retry() { location.href = document.baseURI; }
}
