import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Service, signal } from '@angular/core';
import { catchError, finalize, firstValueFrom, map, Observable, shareReplay, tap, throwError } from 'rxjs';
import * as z from 'zod';
import { AUTH_URL } from './api-config';

export type Role = 'Viewer' | 'Editor' | 'Admin';

/** Weakest first: a higher role may do everything a lower one may. */
const ROLE_ORDER: readonly string[] = ['Viewer', 'Editor', 'Admin'];

/** A token this close to expiring is renewed before it is used, so it does not lapse in flight. */
const EXPIRY_MARGIN_SECONDS = 30;

const accessTokenResponse = z.object({ accessToken: z.string().min(1) });

const claimsSchema = z.object({
  name: z.string(),
  exp: z.number().optional(),
  role: z.union([z.string(), z.array(z.string())]).optional(),
});

/** What a sign-in or registration failure looks like to a screen. */
export class AuthError extends Error {
  constructor(
    message: string,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
  }
}

export interface Registration {
  email: string;
  password: string;
  displayName: string;
}

/**
 * Who is signed in. The access token lives in a signal only, never in web storage, so a reload
 * starts signed out and `restore` renews the session from the refresh cookie. The name and roles
 * are read from the token for the UI; the API is what enforces them.
 */
@Service()
export class Session {
  private readonly http = inject(HttpClient);

  private readonly token = signal<string | null>(null);
  private inFlightRefresh: Observable<string> | null = null;

  readonly accessToken = this.token.asReadonly();
  private readonly claims = computed(() => {
    const token = this.token();
    return token === null ? null : readClaims(token);
  });
  readonly isSignedIn = computed(() => this.claims() !== null);
  /** The Display name on the Account, from the token. */
  readonly name = computed(() => this.claims()?.name ?? null);
  readonly roles = computed<string[]>(() => {
    const role = this.claims()?.role;
    return role === undefined ? [] : Array.isArray(role) ? role : [role];
  });
  readonly canEdit = computed(() => this.hasRole('Editor'));

  hasRole(required: Role): boolean {
    const needed = ROLE_ORDER.indexOf(required);
    return this.roles().some((role) => ROLE_ORDER.indexOf(role) >= needed && ROLE_ORDER.indexOf(role) !== -1);
  }

  async signIn(email: string, password: string): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.post<unknown>(`${AUTH_URL}/sign-in`, { email, password }, { withCredentials: true }),
      );
      this.token.set(accessTokenResponse.parse(response).accessToken);
    } catch (error) {
      throw toAuthError(error);
    }
  }

  /** Register does not sign in, so this signs in afterwards. */
  async register(registration: Registration): Promise<void> {
    try {
      await firstValueFrom(this.http.post<unknown>(`${AUTH_URL}/register`, registration));
    } catch (error) {
      throw toAuthError(error);
    }
    await this.signIn(registration.email, registration.password);
  }

  /**
   * A token that is valid now, for callers that cannot retry on a 401 (the live connection). Renews first
   * when the token is missing or about to expire. Rejects, and signs out, when renewal fails.
   */
  async freshAccessToken(): Promise<string> {
    const token = this.token();
    const exp = this.claims()?.exp;
    if (token !== null && exp !== undefined && exp - Date.now() / 1000 > EXPIRY_MARGIN_SECONDS) {
      return token;
    }
    return firstValueFrom(this.refresh());
  }

  /** Renews the session from the refresh cookie once on load. Never fails: no cookie means signed out. */
  async restore(): Promise<void> {
    try {
      await firstValueFrom(this.refresh());
    } catch {
      // Not signed in.
    }
  }

  /** Trades the refresh cookie for a new access token. Callers at the same time share one request. */
  refresh(): Observable<string> {
    this.inFlightRefresh ??= this.http
      .post<unknown>(`${AUTH_URL}/refresh`, {}, { withCredentials: true })
      .pipe(
        map((response) => accessTokenResponse.parse(response).accessToken),
        tap((token) => this.token.set(token)),
        catchError((error: unknown) => {
          this.clear();
          return throwError(() => error);
        }),
        finalize(() => (this.inFlightRefresh = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.inFlightRefresh;
  }

  /** Ends the session on the server, then here. The Collaborator is signed out even if the server is unreachable. */
  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`${AUTH_URL}/logout`, {}, { withCredentials: true }));
    } catch {
      // The local session is cleared either way.
    }
    this.clear();
  }

  clear(): void {
    this.token.set(null);
  }
}

function readClaims(token: string): z.infer<typeof claimsSchema> | null {
  try {
    const payload = token.split('.')[1] ?? '';
    const base64 = payload.replaceAll('-', '+').replaceAll('_', '/');
    const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
    const parsed = claimsSchema.safeParse(JSON.parse(new TextDecoder().decode(bytes)));
    return parsed.success ? parsed.data : null;
  } catch {
    return null;
  }
}

function toAuthError(error: unknown): AuthError {
  if (!(error instanceof HttpErrorResponse)) {
    return new AuthError('Something went wrong.');
  }
  const body = error.error as { detail?: unknown; errors?: Record<string, string[]> } | null;
  const detail = typeof body?.detail === 'string' ? body.detail : null;
  switch (error.status) {
    case 0:
      return new AuthError('Cannot reach the server. Check that the API is running.');
    case 400:
      return new AuthError('Some fields need attention.', body?.errors ?? {});
    case 401:
    case 423:
      return new AuthError(detail ?? 'Could not sign in.');
    case 429:
      return new AuthError('Too many attempts. Wait a minute and try again.');
    default:
      return new AuthError('The server could not complete the request.');
  }
}
