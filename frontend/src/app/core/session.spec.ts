import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Session } from './session';
import { fakeAccessToken } from './session.testing';

const AUTH = 'http://localhost:5114/api/auth';

describe('Session', () => {
  let session: Session;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    session = TestBed.inject(Session);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const token = (name = 'Ada', role: string | string[] = 'Editor') => ({
    accessToken: fakeAccessToken(name, role),
    expiresIn: 900,
  });

  async function signedIn(name = 'Ada', role: string | string[] = 'Editor'): Promise<void> {
    const done = session.signIn('ada@example.com', 'a long enough password');
    http.expectOne(`${AUTH}/sign-in`).flush(token(name, role));
    await done;
  }

  it('starts signed out', () => {
    expect(session.isSignedIn()).toBe(false);
    expect(session.name()).toBeNull();
    expect(session.roles()).toEqual([]);
  });

  describe('signIn', () => {
    it('posts the credentials with cookies and exposes the name and role from the token', async () => {
      const done = session.signIn('ada@example.com', 'a long enough password');
      const req = http.expectOne(`${AUTH}/sign-in`);
      expect(req.request.method).toBe('POST');
      expect(req.request.withCredentials).toBe(true);
      expect(req.request.body).toEqual({
        email: 'ada@example.com',
        password: 'a long enough password',
      });
      req.flush(token('Ada Lovelace', 'Editor'));
      await done;

      expect(session.isSignedIn()).toBe(true);
      expect(session.name()).toBe('Ada Lovelace');
      expect(session.roles()).toEqual(['Editor']);
    });

    it('keeps the token out of localStorage and sessionStorage', async () => {
      await signedIn();

      const stored = JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage });
      expect(stored).not.toContain(session.accessToken()!);
      expect(localStorage.length + sessionStorage.length).toBe(0);
    });

    it('reads a token whose name has non-ASCII characters', async () => {
      await signedIn('Zoë Núñez');
      expect(session.name()).toBe('Zoë Núñez');
    });

    it('shows the generic message for a wrong email or password and stays signed out', async () => {
      const done = session.signIn('ada@example.com', 'wrong');
      http
        .expectOne(`${AUTH}/sign-in`)
        .flush(
          { title: 'Unauthorized', detail: 'Invalid email or password.' },
          { status: 401, statusText: 'Unauthorized' },
        );

      await expect(done).rejects.toMatchObject({ message: 'Invalid email or password.' });
      expect(session.isSignedIn()).toBe(false);
    });

    it('shows the lockout message for a locked account', async () => {
      const done = session.signIn('ada@example.com', 'wrong');
      http
        .expectOne(`${AUTH}/sign-in`)
        .flush(
          {
            detail:
              'This account is locked after too many failed sign-ins. Please try again in 15 minutes.',
          },
          { status: 423, statusText: 'Locked' },
        );

      await expect(done).rejects.toMatchObject({
        message:
          'This account is locked after too many failed sign-ins. Please try again in 15 minutes.',
      });
    });

    it('says the server cannot be reached when there is no response', async () => {
      const done = session.signIn('ada@example.com', 'x');
      http.expectOne(`${AUTH}/sign-in`).error(new ProgressEvent('error'));

      await expect(done).rejects.toMatchObject({
        message: expect.stringContaining('Cannot reach the server'),
      });
    });

    it('says to slow down when rate limited', async () => {
      const done = session.signIn('ada@example.com', 'x');
      http
        .expectOne(`${AUTH}/sign-in`)
        .flush(null, { status: 429, statusText: 'Too Many Requests' });

      await expect(done).rejects.toMatchObject({
        message: expect.stringContaining('Too many attempts'),
      });
    });
  });

  describe('roles', () => {
    it('treats Admin as Editor and Editor as Viewer, but not the other way', async () => {
      await signedIn('Ada', 'Editor');
      expect(session.hasRole('Viewer')).toBe(true);
      expect(session.hasRole('Editor')).toBe(true);
      expect(session.hasRole('Admin')).toBe(false);
      expect(session.canEdit()).toBe(true);
    });

    it('lets a Viewer read but not edit', async () => {
      await signedIn('Vic', 'Viewer');
      expect(session.hasRole('Viewer')).toBe(true);
      expect(session.canEdit()).toBe(false);
    });

    it('lets an Admin edit', async () => {
      await signedIn('Root', 'Admin');
      expect(session.canEdit()).toBe(true);
    });

    it('accepts several roles in the token', async () => {
      await signedIn('Ada', ['Viewer', 'Editor']);
      expect(session.roles()).toEqual(['Viewer', 'Editor']);
      expect(session.canEdit()).toBe(true);
    });
  });

  describe('register', () => {
    it('creates the account, then signs in because register does not', async () => {
      const done = session.register({
        email: 'ada@example.com',
        password: 'a long enough password',
        displayName: 'Ada',
      });
      const create = http.expectOne(`${AUTH}/register`);
      expect(create.request.body).toEqual({
        email: 'ada@example.com',
        password: 'a long enough password',
        displayName: 'Ada',
      });
      expect(session.isSignedIn()).toBe(false);
      create.flush({ id: '1' }, { status: 201, statusText: 'Created' });
      await Promise.resolve();
      http.expectOne(`${AUTH}/sign-in`).flush(token('Ada'));
      await done;

      expect(session.name()).toBe('Ada');
    });

    it('reports field messages from a 400 and does not sign in', async () => {
      const done = session.register({
        email: 'ada@example.com',
        password: 'short',
        displayName: 'Ada',
      });
      http
        .expectOne(`${AUTH}/register`)
        .flush(
          {
            errors: {
              password: ['Passwords must be at least 12 characters.'],
              email: ['Email is taken.'],
            },
          },
          { status: 400, statusText: 'Bad Request' },
        );

      await expect(done).rejects.toMatchObject({
        fieldErrors: {
          password: ['Passwords must be at least 12 characters.'],
          email: ['Email is taken.'],
        },
      });
      expect(session.isSignedIn()).toBe(false);
    });
  });

  describe('restore', () => {
    it('signs in again from the refresh cookie on load', async () => {
      const done = session.restore();
      const req = http.expectOne(`${AUTH}/refresh`);
      expect(req.request.withCredentials).toBe(true);
      req.flush(token('Ada'));
      await done;

      expect(session.name()).toBe('Ada');
    });

    it('stays signed out, without failing, when there is no valid cookie', async () => {
      const done = session.restore();
      http.expectOne(`${AUTH}/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });
      await done;

      expect(session.isSignedIn()).toBe(false);
    });
  });

  describe('refresh', () => {
    it('shares one request between concurrent callers', async () => {
      await signedIn();
      const results: string[] = [];
      session.refresh().subscribe((t) => results.push(t));
      session.refresh().subscribe((t) => results.push(t));

      const renewed = token('Ada');
      http.expectOne(`${AUTH}/refresh`).flush(renewed);

      expect(results).toEqual([renewed.accessToken, renewed.accessToken]);
      expect(session.accessToken()).toBe(renewed.accessToken);
    });

    it('starts a new request once the previous one finished', async () => {
      session.refresh().subscribe();
      http.expectOne(`${AUTH}/refresh`).flush(token('Ada'));
      session.refresh().subscribe();
      http.expectOne(`${AUTH}/refresh`).flush(token('Ada'));
    });

    it('signs out when the refresh is refused', async () => {
      await signedIn();
      let failed = false;
      session.refresh().subscribe({ error: () => (failed = true) });
      http.expectOne(`${AUTH}/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(failed).toBe(true);
      expect(session.isSignedIn()).toBe(false);
    });
  });

  describe('signOut', () => {
    it('ends the session on the server with the cookie and clears it here', async () => {
      await signedIn();
      const done = session.signOut();
      const req = http.expectOne(`${AUTH}/logout`);
      expect(req.request.method).toBe('POST');
      expect(req.request.withCredentials).toBe(true);
      req.flush(null, { status: 204, statusText: 'No Content' });
      await done;

      expect(session.isSignedIn()).toBe(false);
      expect(session.name()).toBeNull();
    });

    it('still clears the session when the server cannot be reached', async () => {
      await signedIn();
      const done = session.signOut();
      http.expectOne(`${AUTH}/logout`).error(new ProgressEvent('error'));
      await done;

      expect(session.isSignedIn()).toBe(false);
    });
  });
});
