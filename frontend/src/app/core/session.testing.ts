import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Session } from './session';

/** Test helper: an unsigned token shaped like the API's access token. The app only reads its claims for the UI. */
export function fakeAccessToken(name: string, role: string | string[] = 'Editor', expiresInSeconds = 900): string {
  const encode = (value: object) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replaceAll('+', '-')
      .replaceAll('/', '_')
      .replaceAll('=', '');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: '1', name, role, exp: Math.floor(Date.now() / 1000) + expiresInSeconds })}.signature`;
}

/** Test helper: signs in through the real Session with the sign-in request answered by a fake token. */
export async function signInAs(name: string, role: string | string[] = 'Editor', expiresInSeconds = 900): Promise<void> {
  const done = TestBed.inject(Session).signIn('test@example.com', 'a long enough password');
  TestBed.inject(HttpTestingController)
    .expectOne('http://localhost:5114/api/auth/sign-in')
    .flush({ accessToken: fakeAccessToken(name, role, expiresInSeconds), expiresIn: 900 });
  await done;
}
