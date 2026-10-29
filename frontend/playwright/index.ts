import { provideHttpClient } from '@angular/common/http';
import { inject, provideEnvironmentInitializer } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { Session } from '../src/app/core/session';
import { beforeMount } from '@sand4rt/experimental-ct-angular/hooks';

declare global {
  interface Window {
    /** The Router a mounted page uses, so a test can read where it navigated. */
    __pwRouter?: Router;
    /** The Session a mounted page uses, so a test can sign in as a role. */
    __pwSession?: Session;
  }
}

// Pages call the real HttpClient and Router. A test answers HTTP with page.route and reads the Router for navigation.
beforeMount(async ({ TestBed }) => {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([{ path: '**', children: [] }]),
      provideEnvironmentInitializer(() => {
        window.__pwRouter = inject(Router);
        window.__pwSession = inject(Session);
      }),
    ],
  });
});
