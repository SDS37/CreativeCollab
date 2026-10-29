import { Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthError, Session } from '../../core/session';

/** Where to go after sign-in: the page the Collaborator wanted, if it is inside the app. */
function destination(router: Router): string {
  const wanted = router.parseUrl(router.url).queryParams['returnUrl'];
  return typeof wanted === 'string' && wanted.startsWith('/') && !wanted.startsWith('//') && !wanted.includes('\\')
    ? wanted
    : '/creatives';
}

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <h1>Sign in</h1>
      <label for="login-email">Email</label>
      <input id="login-email" type="email" formControlName="email" autocomplete="username" />
      <label for="login-password">Password</label>
      <input id="login-password" type="password" formControlName="password" autocomplete="current-password" />
      @if (error(); as message) {
        <p role="alert" class="error">{{ message }}</p>
      }
      <button type="submit" [disabled]="!canSubmit()">{{ submitting() ? 'Signing in…' : 'Sign in' }}</button>
      <p>No account yet? <a routerLink="/register">Create an account</a></p>
    </form>
  `,
  styles: `
    form { display: grid; gap: 0.5rem; max-width: 20rem; margin: 2rem auto; }
    input { font: inherit; padding: 0.5rem; }
    .error { color: #b00020; margin: 0; }
  `,
})
export class LoginPage {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly valid = signal(false);

  protected readonly canSubmit = computed(() => this.valid() && !this.submitting());

  private readonly track = this.form.statusChanges.subscribe(() => this.valid.set(this.form.valid));

  protected async submit(): Promise<void> {
    if (!this.form.valid || this.submitting()) {
      return;
    }
    const { email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.session.signIn(email.trim(), password);
      await this.router.navigateByUrl(destination(this.router));
    } catch (error) {
      this.error.set(error instanceof AuthError ? error.message : 'Something went wrong.');
    } finally {
      this.submitting.set(false);
    }
  }
}
