import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { appConfig } from './app.config';
import { Session } from './core/session';
import { fakeAccessToken } from './core/session.testing';

const AUTH = 'http://localhost:5114/api/auth';

describe('app wiring', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function started(signedInAs?: string, role = 'Editor'): Promise<void> {
    const init = TestBed.inject(ApplicationInitStatus);
    const request = http.expectOne(`${AUTH}/refresh`);
    if (signedInAs) {
      request.flush({ accessToken: fakeAccessToken(signedInAs, role), expiresIn: 900 });
    } else {
      request.flush({}, { status: 401, statusText: 'Unauthorized' });
    }
    await init.donePromise;
  }

  it('restores the session from the refresh cookie before the app starts', async () => {
    await started('Ada');

    expect(TestBed.inject(Session).name()).toBe('Ada');
  });

  it('starts signed out when there is no valid refresh cookie', async () => {
    await started();

    expect(TestBed.inject(Session).isSignedIn()).toBe(false);
  });

  it('sends the restored token on API requests', async () => {
    await started('Ada');

    TestBed.inject(HttpClient).get('http://localhost:5114/api/creatives').subscribe();

    const req = http.expectOne('http://localhost:5114/api/creatives');
    expect(req.request.headers.get('Authorization')).toMatch(/^Bearer /);
  });

  describe('routes', () => {
    it('sends a signed-out visitor to sign-in and remembers the page', async () => {
      await started();

      await TestBed.inject(Router).navigateByUrl('/creatives/3');

      expect(TestBed.inject(Router).url).toBe('/sign-in?returnUrl=%2Fcreatives%2F3');
    });

    it('lets a Viewer open the list and a Creative but not the create page', async () => {
      await started('Vic', 'Viewer');
      const router = TestBed.inject(Router);

      await router.navigateByUrl('/creatives/3');
      expect(router.url).toBe('/creatives/3');

      await router.navigateByUrl('/creatives/new');
      expect(router.url).toBe('/creatives');
    });

    it('lets an Editor open the create page', async () => {
      await started('Ed', 'Editor');
      const router = TestBed.inject(Router);

      await router.navigateByUrl('/creatives/new');

      expect(router.url).toBe('/creatives/new');
    });

    it('keeps sign-in and register open to everyone', async () => {
      await started();
      const router = TestBed.inject(Router);

      await router.navigateByUrl('/register');
      expect(router.url).toBe('/register');
      await router.navigateByUrl('/sign-in');
      expect(router.url).toBe('/sign-in');
    });
  });
});
