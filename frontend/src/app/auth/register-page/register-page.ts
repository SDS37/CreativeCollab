import { Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthError, Session } from '../../core/session';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <h1>Create an account</h1>
      <label for="register-name">Display name</label>
      <input id="register-name" type="text" formControlName="displayName" autocomplete="name" maxlength="100" />
      @for (message of fieldErrors()['displayName']; track message) {
        <p role="alert" class="error">{{ message }}</p>
      }
      <label for="register-email">Email</label>
      <input id="register-email" type="email" formControlName="email" autocomplete="username" />
      @for (message of fieldErrors()['email']; track message) {
        <p role="alert" class="error">{{ message }}</p>
      }
      <label for="register-password">Password</label>
      <input id="register-password" type="password" formControlName="password" autocomplete="new-password" />
      <p class="hint">Use at least 12 characters. A passphrase works well.</p>
      @for (message of fieldErrors()['password']; track message) {
        <p role="alert" class="error">{{ message }}</p>
      }
      @if (error(); as message) {
        <p role="alert" class="error">{{ message }}</p>
      }
      <button type="submit" [disabled]="!canSubmit()">{{ submitting() ? 'Creating…' : 'Create account' }}</button>
      <p>Already have an account? <a routerLink="/login">Sign in</a></p>
    </form>
  `,
  styles: `
    form { display: grid; gap: 0.5rem; max-width: 20rem; margin: 2rem auto; }
    input { font: inherit; padding: 0.5rem; }
    .error { color: #b00020; margin: 0; }
    .hint { color: #555; margin: 0; font-size: 0.9em; }
  `,
})
export class RegisterPage {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required]],
    email: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly fieldErrors = signal<Record<string, string[]>>({});
  private readonly valid = signal(false);

  protected readonly canSubmit = computed(() => this.valid() && !this.submitting());

  private readonly track = this.form.statusChanges.subscribe(() => this.valid.set(this.form.valid));

  protected async submit(): Promise<void> {
    if (!this.form.valid || this.submitting()) {
      return;
    }
    const { displayName, email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);
    this.fieldErrors.set({});
    try {
      await this.session.register({ displayName: displayName.trim(), email: email.trim(), password });
      await this.router.navigateByUrl('/creatives');
    } catch (error) {
      if (error instanceof AuthError && Object.keys(error.fieldErrors).length > 0) {
        this.fieldErrors.set(error.fieldErrors);
      } else {
        this.error.set(error instanceof AuthError ? error.message : 'Something went wrong.');
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
