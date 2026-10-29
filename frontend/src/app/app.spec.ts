import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { Session } from './core/session';
import { signInAs } from './core/session.testing';

describe('App shell', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;

  function create(): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(App);
  }

  afterEach(() => http.verify());

  it('shows no account header while signed out', async () => {
    create();
    await fixture.whenStable();

    expect(el().querySelector('header.shell')).toBeNull();
    expect(el().querySelector('router-outlet')).not.toBeNull();
  });

  it('shows the account name from the session, with no way to rename it', async () => {
    create();
    await signInAs('Ada Lovelace');
    await fixture.whenStable();

    expect(el().textContent).toContain('Signed in as Ada Lovelace');
    expect(el().querySelector('.change-name')).toBeNull();
    expect(el().querySelector('app-display-name-prompt')).toBeNull();
  });

  it('signs out on the server, clears the session and goes to login', async () => {
    create();
    await signInAs('Ada');
    await fixture.whenStable();

    (el().querySelector('button.sign-out') as HTMLButtonElement).click();
    const logout = http.expectOne('http://localhost:5114/api/auth/logout');
    expect(logout.request.method).toBe('POST');
    logout.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(TestBed.inject(Session).isSignedIn()).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(el().querySelector('header.shell')).toBeNull();
  });
});
