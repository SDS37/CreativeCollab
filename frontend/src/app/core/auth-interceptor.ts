import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, switchMap, throwError } from 'rxjs';
import { API_ORIGIN, AUTH_URL } from './api-config';
import { Session } from './session';

/** These answer 401 for their own reasons (wrong password, no cookie), so they are never renewed or retried. */
const ANONYMOUS = ['login', 'register', 'refresh'].map((name) => `${AUTH_URL}/${name}`);

const withBearer = (request: HttpRequest<unknown>, token: string | null) =>
  token === null ? request : request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

/**
 * Adds the access token to requests to the API and nothing else. A 401 renews the session once
 * (concurrent requests share the renewal) and retries the request once. If that fails the
 * Collaborator is sent to login.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(API_ORIGIN) || ANONYMOUS.includes(request.url)) {
    return next(request);
  }
  const session = inject(Session);
  const router = inject(Router);

  const sentWith = session.accessToken();
  const retry = (token: string): Observable<HttpEvent<unknown>> =>
    next(withBearer(request, token)).pipe(
      catchError((error: unknown) => (isUnauthorized(error) ? fail(error) : throwError(() => error))),
    );
  const fail = (error: unknown): Observable<never> => {
    session.clear();
    if (!router.url.startsWith('/login')) {
      void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
    }
    return throwError(() => error);
  };

  return next(withBearer(request, sentWith)).pipe(
    catchError((error: unknown) => {
      if (!isUnauthorized(error)) {
        return throwError(() => error);
      }
      const current = session.accessToken();
      // Another request already renewed the session while this one was in flight.
      if (current !== null && current !== sentWith) {
        return retry(current);
      }
      return session.refresh().pipe(
        catchError(() => fail(error)),
        switchMap((token) => retry(token)),
      );
    }),
  );
};

function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}
