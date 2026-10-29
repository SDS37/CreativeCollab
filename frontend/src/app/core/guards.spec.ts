import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authGuard, roleGuard } from './guards';
import { Session } from './session';
import { fakeAccessToken } from './session.testing';

const AUTH = 'http://localhost:5114/api/auth';

describe('route guards', () => {
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'login', children: [] },
          { path: 'creatives', canActivate: [authGuard], children: [] },
          { path: 'creatives/new', canActivate: [authGuard, roleGuard('Editor')], children: [] },
        ]),
      ],
    });
    router = TestBed.inject(Router);
  });

  async function signIn(role: string): Promise<void> {
    const session = TestBed.inject(Session);
    const done = session.signIn('a@example.com', 'a long enough password');
    TestBed.inject(HttpTestingController)
      .expectOne(`${AUTH}/login`)
      .flush({ accessToken: fakeAccessToken('Ada', role), expiresIn: 900 });
    await done;
  }

  it('sends a signed-out Collaborator to login and remembers the page they wanted', async () => {
    await router.navigateByUrl('/creatives');

    expect(router.url).toBe('/login?returnUrl=%2Fcreatives');
  });

  it('keeps the whole wanted URL, query included', async () => {
    await router.navigateByUrl('/creatives?x=1');

    expect(router.url).toBe('/login?returnUrl=%2Fcreatives%3Fx%3D1');
  });

  it('lets a signed-in Collaborator in', async () => {
    await signIn('Viewer');

    await router.navigateByUrl('/creatives');

    expect(router.url).toBe('/creatives');
  });

  it('sends a signed-out Collaborator to login from a role-guarded page too', async () => {
    await router.navigateByUrl('/creatives/new');

    expect(router.url).toBe('/login?returnUrl=%2Fcreatives%2Fnew');
  });

  it('blocks a Viewer from a page that needs an Editor and sends them to the list', async () => {
    await signIn('Viewer');

    await router.navigateByUrl('/creatives/new');

    expect(router.url).toBe('/creatives');
  });

  it('lets an Editor in', async () => {
    await signIn('Editor');

    await router.navigateByUrl('/creatives/new');

    expect(router.url).toBe('/creatives/new');
  });

  it('lets an Admin in where an Editor may go', async () => {
    await signIn('Admin');

    await router.navigateByUrl('/creatives/new');

    expect(router.url).toBe('/creatives/new');
  });
});
