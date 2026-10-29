import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { Session } from './core/session';
import { SIGN_IN_PATH } from './core/sign-in-redirect';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly session = inject(Session);
  private readonly router = inject(Router);

  protected async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigate([SIGN_IN_PATH]);
  }
}
