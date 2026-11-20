import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** The root only holds the router: the login page, the "no access" page and the app frame (layout/shell.ts) are routes. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class App {}
