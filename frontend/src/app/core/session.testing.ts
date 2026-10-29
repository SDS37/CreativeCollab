/** Test helper: an unsigned token shaped like the API's access token. The app only reads its claims for the UI. */
export function fakeAccessToken(name: string, role: string | string[] = 'Editor'): string {
  const encode = (value: object) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replaceAll('+', '-')
      .replaceAll('/', '_')
      .replaceAll('=', '');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: '1', name, role })}.signature`;
}
