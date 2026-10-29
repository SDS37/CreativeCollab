import { Router, UrlTree } from '@angular/router';

export const SIGN_IN_PATH = '/sign-in';

/** The sign-in page, remembering the page the Collaborator was headed for. */
export const signInUrl = (router: Router, returnUrl: string): UrlTree =>
  router.createUrlTree([SIGN_IN_PATH], { queryParams: { returnUrl } });
