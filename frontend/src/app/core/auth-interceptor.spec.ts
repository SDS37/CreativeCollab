import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authInterceptor } from './auth-interceptor';
import { Session } from './session';
import { fakeAccessToken } from './session.testing';

const API = 'http://localhost:5114/api/creatives';
const AUTH = 'http://localhost:5114/api/auth';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let session: Session;
  let router: Router;

  const first = fakeAccessToken('Ada', 'Editor');
  const second = fakeAccessToken('Ada', 'Editor') + 'x';

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    session = TestBed.inject(Session);
    router = TestBed.inject(Router);

    const done = session.signIn('ada@example.com', 'a long enough password');
    controller.expectOne(`${AUTH}/sign-in`).flush({ accessToken: first, expiresIn: 900 });
    await done;
  });

  afterEach(() => controller.verify());

  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  it('adds the bearer token to a request to the API', () => {
    http.get(API).subscribe();

    expect(controller.expectOne(API).request.headers.get('Authorization')).toBe(`Bearer ${first}`);
    controller.match(API);
  });

  it('adds no token to a request to another origin', () => {
    http.get('https://example.com/data').subscribe();

    expect(controller.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
  });

  it.each([
    'http://localhost:5114@evil.com/x',
    'http://localhost:5114.evil.com/x',
    'http://localhost:51140/x',
    'https://localhost:5114/x',
    'relative/path',
    '/api/creatives',
  ])('adds no token to %s, which is not the API origin', (url) => {
    http.get(url).subscribe({ error: () => {} });

    expect(controller.expectOne(url).request.headers.has('Authorization')).toBe(false);
  });

  it('still adds the token to the API origin however the URL is written', () => {
    for (const url of [API, `${API}?page=1`, 'http://LOCALHOST:5114/api/creatives', 'http://localhost:5114']) {
      http.get(url).subscribe({ error: () => {} });
      expect(controller.expectOne(url).request.headers.get('Authorization')).toBe(`Bearer ${first}`);
    }
  });

  it('does not send the token to the sign-in, register or refresh endpoints', () => {
    for (const path of ['sign-in', 'register', 'refresh']) {
      http.post(`${AUTH}/${path}`, {}).subscribe({ error: () => {} });
      expect(controller.expectOne(`${AUTH}/${path}`).request.headers.has('Authorization')).toBe(false);
    }
  });

  it('does not try to renew when sign-in itself answers 401', () => {
    let status = 0;
    http.post(`${AUTH}/sign-in`, {}).subscribe({ error: (e) => (status = e.status) });
    controller.expectOne(`${AUTH}/sign-in`).flush({}, unauthorized);

    expect(status).toBe(401);
    controller.expectNone(`${AUTH}/refresh`);
  });

  it('signs out from the refresh cookie alone: no bearer, and an expired token is not renewed just to log out', async () => {
    const done = session.signOut();
    const logout = controller.expectOne(`${AUTH}/logout`);
    expect(logout.request.headers.has('Authorization')).toBe(false);
    logout.flush({}, unauthorized);
    await done;

    controller.expectNone(`${AUTH}/refresh`);
    expect(session.isSignedIn()).toBe(false);
  });

  it('renews once on a 401, then retries the request with the new token', () => {
    let result: unknown;
    http.get(API).subscribe((r) => (result = r));
    controller.expectOne(API).flush({}, unauthorized);

    controller.expectOne(`${AUTH}/refresh`).flush({ accessToken: second, expiresIn: 900 });
    const retry = controller.expectOne(API);
    expect(retry.request.headers.get('Authorization')).toBe(`Bearer ${second}`);
    retry.flush({ ok: true });

    expect(result).toEqual({ ok: true });
  });

  it('shares one renewal between requests that fail together', () => {
    const results: unknown[] = [];
    http.get(`${API}/1`).subscribe((r) => results.push(r));
    http.get(`${API}/2`).subscribe((r) => results.push(r));
    controller.expectOne(`${API}/1`).flush({}, unauthorized);
    controller.expectOne(`${API}/2`).flush({}, unauthorized);

    controller.expectOne(`${AUTH}/refresh`).flush({ accessToken: second, expiresIn: 900 });
    controller.expectOne(`${API}/1`).flush('one');
    controller.expectOne(`${API}/2`).flush('two');

    expect(results).toEqual(['one', 'two']);
  });

  it('retries with the token already renewed instead of renewing again', () => {
    http.get(`${API}/1`).subscribe();
    http.get(`${API}/2`).subscribe();
    const late = controller.expectOne(`${API}/2`);
    controller.expectOne(`${API}/1`).flush({}, unauthorized);
    controller.expectOne(`${AUTH}/refresh`).flush({ accessToken: second, expiresIn: 900 });
    controller.expectOne(`${API}/1`).flush('one');

    late.flush({}, unauthorized);

    const retry = controller.expectOne(`${API}/2`);
    expect(retry.request.headers.get('Authorization')).toBe(`Bearer ${second}`);
    retry.flush('two');
    controller.expectNone(`${AUTH}/refresh`);
  });

  it('retries only once: a second 401 clears the session and goes to sign-in', async () => {
    let status = 0;
    http.get(API).subscribe({ error: (e) => (status = e.status) });
    controller.expectOne(API).flush({}, unauthorized);
    controller.expectOne(`${AUTH}/refresh`).flush({ accessToken: second, expiresIn: 900 });
    controller.expectOne(API).flush({}, unauthorized);

    expect(status).toBe(401);
    controller.expectNone(`${AUTH}/refresh`);
    expect(session.isSignedIn()).toBe(false);
    await vi.waitFor(() => expect(router.url).toMatch(/^\/sign-in/));
  });

  it('clears the session and goes to sign-in, remembering the page, when renewal fails', async () => {
    await router.navigateByUrl('/creatives/4');
    let status = 0;
    http.get(API).subscribe({ error: (e) => (status = e.status) });
    controller.expectOne(API).flush({}, unauthorized);
    controller.expectOne(`${AUTH}/refresh`).flush({}, unauthorized);

    expect(status).toBe(401);
    expect(session.isSignedIn()).toBe(false);
    await vi.waitFor(() => expect(router.url).toBe('/sign-in?returnUrl=%2Fcreatives%2F4'));
  });

  it('passes other errors through without renewing', () => {
    let status = 0;
    http.get(API).subscribe({ error: (e) => (status = e.status) });
    controller.expectOne(API).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    controller.expectNone(`${AUTH}/refresh`);
    expect(session.isSignedIn()).toBe(true);
  });
});
