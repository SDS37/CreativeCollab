import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AuthError, Session } from '../../core/session';

/** Where to go after sign-in: the page the Collaborator wanted, if it is inside the app. */
function destination(router: Router): string {
  const wanted = router.parseUrl(router.url).queryParams['returnUrl'];
  return typeof wanted === 'string' && wanted.startsWith('/') && !wanted.startsWith('//') && !wanted.includes('\\')
    ? wanted
    : '/creatives';
}

@Component({
  selector: 'app-sign-in-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <h1>Sign in</h1>
      <label for="sign-in-email">Email</label>
      <input id="sign-in-email" type="email" formControlName="email" autocomplete="username" />
      <label for="sign-in-password">Password</label>
      <input id="sign-in-password" type="password" formControlName="password" autocomplete="current-password" />
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
export class SignInPage {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Form validity is not a signal, so it is read again on every status change. */
  private readonly valid = toSignal(this.form.statusChanges.pipe(map(() => this.form.valid)), {
    initialValue: false,
  });

  protected readonly canSubmit = computed(() => this.valid() && !this.submitting());

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
