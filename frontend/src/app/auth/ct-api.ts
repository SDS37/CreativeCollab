import { Page, Route } from '@sand4rt/experimental-ct-angular';

/** Shared by the auth screen component tests: the API answers on another origin, with credentials. */
export const AUTH = 'http://localhost:5114/api/auth';

/** The component test page is served from here; credentialed CORS needs this origin, not `*`. */
const cors = {
  'access-control-allow-origin': 'http://localhost:3100',
  'access-control-allow-credentials': 'true',
  'access-control-allow-headers': 'content-type',
  'access-control-allow-methods': 'POST, OPTIONS',
};

/** Answers a POST to an auth endpoint, and its CORS preflight. */
export async function answer(
  page: Page,
  path: string,
  respond: (route: Route) => Promise<void> | void,
): Promise<void> {
  await page.route(`${AUTH}/${path}`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: cors });
      return;
    }
    await respond(route);
  });
}

export const reply = (route: Route, status: number, json?: unknown) =>
  route.fulfill({ status, headers: cors, ...(json === undefined ? {} : { json }) });

export const signedIn = { accessToken: 'header.payload.signature', expiresIn: 900 };
