import { Component, OnDestroy, inject, signal } from '@angular/core';
import { AuthService } from '../../core/services';

/**
 * The home page: three pictures as a carousel inside the app frame (top bar + side pane). It opens first after sign-in
 * (once per browser session) and whenever the logo is pressed. To change the pictures replace the files in client/public/homepage
 * and edit `pics`.
 */
@Component({
  selector: 'app-home',
  template: `
    <section class="dd-carousel" aria-roledescription="carousel" (mouseenter)="paused = true" (mouseleave)="paused = false"
             (touchstart)="touchX = $event.touches[0].clientX" (touchend)="swipe($event.changedTouches[0].clientX)">
      <div class="dd-stage">
        <div class="dd-track" [style.transform]="'translateX(' + -index() * 100 + '%)'">
          @for (p of pics; track p.src; let i = $index) {
            <div class="dd-slide" role="group" [attr.aria-label]="p.alt" [attr.aria-hidden]="i !== index()">
              <img [src]="p.src" [alt]="p.alt" draggable="false" [attr.loading]="i === 0 ? 'eager' : 'lazy'">
            </div>
          }
        </div>
        <button type="button" class="dd-arrow prev" (click)="step(-1)" aria-label="Previous"><i class="bi bi-chevron-left"></i></button>
        <button type="button" class="dd-arrow next" (click)="step(1)" aria-label="Next"><i class="bi bi-chevron-right"></i></button>
      </div>
      <div class="dd-dots">
        @for (p of pics; track p.src; let i = $index) {
          <button type="button" class="dd-dot" [class.on]="i === index()" (click)="show(i)" [attr.aria-label]="'Picture ' + (i + 1)"></button>
        }
      </div>
    </section>
  `,
})
export class Home implements OnDestroy {
  private auth = inject(AuthService);
  readonly pics = [
    { src: 'homepage/1.jpg', alt: 'Domain shared folder, file types and quota, secure download' },
    { src: 'homepage/2.jpg', alt: 'Internet to intranet through the data diode: one-way flow, no data return' },
    { src: 'homepage/3.jpg', alt: 'Internet, secure transfer, air-gapped network' },
  ];
  readonly index = signal(0);
  paused = false;
  touchX = 0;
  private readonly timer = setInterval(() => { if (!this.paused) this.step(1); }, 6000);

  constructor() { this.auth.markHomepageSeen(); }

  ngOnDestroy() { clearInterval(this.timer); }

  step(d: number) { this.index.update(i => (i + d + this.pics.length) % this.pics.length); }
  show(i: number) { this.index.set(i); }
  swipe(endX: number) { const dx = endX - this.touchX; if (Math.abs(dx) > 40) this.step(dx < 0 ? 1 : -1); }
}
