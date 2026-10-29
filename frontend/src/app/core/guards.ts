import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role, Session } from './session';

/**
 * Route guards are UX only: they spare the Collaborator a page the API would refuse.
 * The API enforces sign-in and roles on every call.
 */

/** Signed-in Collaborators only. Others go to sign-in and come back to the page they wanted. */
export const authGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  return inject(Session).isSignedIn()
    ? true
    : router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
};

/** Collaborators with at least this role. A signed-in Collaborator without it goes to the list. */
export const roleGuard =
  (role: Role): CanActivateFn =>
  (_route, state) => {
    const router = inject(Router);
    const session = inject(Session);
    if (!session.isSignedIn()) {
      return router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
    }
    return session.hasRole(role) ? true : router.createUrlTree(['/creatives']);
  };
