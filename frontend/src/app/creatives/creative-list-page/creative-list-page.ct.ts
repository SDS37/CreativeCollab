import { expect, test } from '@sand4rt/experimental-ct-angular';
import type { Page } from 'playwright/test';
import { answer, reply } from '../../auth/stub-auth-api';
import { fakeAccessToken } from '../../core/session.testing';
import { Creative } from '../../core/creative.model';
import { CreativeListPage } from './creative-list-page';

const API = 'http://localhost:5114/api/creatives';
const cors = { 'access-control-allow-origin': '*' };

function creative(id: number, name: string): Creative {
  return {
    id: String(id),
    name,
    headline: `Headline ${id}`,
    body: 'b',
    cta: 'c',
    imageUrl: null,
    version: id,
    updatedAt: '2026-01-01T00:00:00Z',
    updatedBy: 'Ada',
  };
}

test('shows loading, then the Creatives in the order the API returned them', async ({ mount, page }) => {
  let respond!: () => void;
  const gate = new Promise<void>((resolve) => (respond = resolve));
  await page.route(API, async (route) => {
    await gate;
    await route.fulfill({ json: [creative(2, 'Second'), creative(1, 'First')], headers: cors });
  });

  const component = await mount(CreativeListPage);
  await expect(component).toContainText('Loading');

  respond();

  await expect(component).not.toContainText('Loading');
  await expect(component.locator('.creative-name')).toHaveText(['Second', 'First']);
});

test('says so when there are no Creatives', async ({ mount, page }) => {
  await page.route(API, (route) => route.fulfill({ json: [], headers: cors }));

  const component = await mount(CreativeListPage);

  await expect(component).toContainText('No Creatives yet');
});

test('shows an error with a retry that reloads the list', async ({ mount, page }) => {
  let calls = 0;
  await page.route(API, (route) => {
    calls++;
    return calls === 1 ? route.abort() : route.fulfill({ json: [creative(1, 'First')], headers: cors });
  });

  const component = await mount(CreativeListPage);
  await expect(component).toContainText('Cannot reach the server');

  await component.getByRole('button', { name: 'Try again' }).click();

  await expect(component.locator('.creative-name')).toHaveText(['First']);
  expect(calls).toBe(2);
});

async function signInAs(page: Page, role: string): Promise<void> {
  await answer(page, 'sign-in', (route) =>
    reply(route, 200, { accessToken: fakeAccessToken('Ada', role), expiresIn: 900 }),
  );
  await page.evaluate(() => window.__pwSession?.signIn('ada@example.com', 'a long enough password'));
}

test('offers New Creative to an Editor', async ({ mount, page }) => {
  await page.route(API, (route) => route.fulfill({ json: [], headers: cors }));
  const component = await mount(CreativeListPage);
  await signInAs(page, 'Editor');

  await expect(component.getByRole('link', { name: 'New Creative' })).toBeVisible();
});

test('does not offer New Creative to a Viewer, who can still open Creatives', async ({ mount, page }) => {
  await page.route(API, (route) => route.fulfill({ json: [creative(4, 'Four')], headers: cors }));
  const component = await mount(CreativeListPage);
  await signInAs(page, 'Viewer');

  await expect(component.getByRole('link', { name: 'New Creative' })).toHaveCount(0);
  await expect(component.locator('button.open')).toHaveCount(1);
});

test('opens a Creative when chosen', async ({ mount, page }) => {
  await page.route(API, (route) => route.fulfill({ json: [creative(4, 'Four')], headers: cors }));

  const component = await mount(CreativeListPage);
  await component.locator('button.open').click();

  await expect.poll(() => page.evaluate(() => window.__pwRouter?.url)).toBe('/creatives/4');
});
