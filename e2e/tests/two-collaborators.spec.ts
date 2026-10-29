import { expect, test, type Browser, type Page } from '@playwright/test';
import { register, setRole, signIn, type Account } from './accounts';

async function openAs(browser: Browser, account: Account, url = '/'): Promise<Page> {
  const page = await (await browser.newContext()).newPage();
  await signIn(page, account, url);
  return page;
}

test('two accounts keep a dirty Draft, resolve the Conflict by name, and drop Presence on leave', async ({
  browser,
  request,
}) => {
  const name = `Sale ${Date.now()}`;
  const annAccount = await register(request, 'Ann');
  const bobAccount = await register(request, 'Bob');

  const ann = await openAs(browser, annAccount);
  await ann.getByRole('link', { name: 'New Creative' }).click();
  await ann.getByLabel('Name').fill(name);
  await ann.getByLabel('Headline').fill('Ann headline');
  await ann.getByLabel('Body').fill('Ann body');
  await ann.getByLabel('Call to action').fill('Ann cta');
  await ann.getByRole('button', { name: 'Save' }).click();

  await expect(ann.getByText('Version 1, last saved by Ann')).toBeVisible();
  await expect(ann.getByText('Open now: Ann')).toBeVisible();

  // A full page load: Bob signs in, then opens the same Creative as a restored session.
  const bob = await openAs(browser, bobAccount);
  await bob.goto(ann.url());
  await expect(ann.getByText('Open now: Ann, Bob')).toBeVisible();
  await expect(bob.getByText('Open now: Ann, Bob')).toBeVisible();

  await ann.getByLabel('Body').fill('Ann draft body');
  await bob.getByLabel('Headline').fill('Bob headline');
  await bob.getByRole('button', { name: 'Save' }).click();
  await expect(bob.getByText('Version 2, last saved by Bob')).toBeVisible();

  await expect(ann.getByText('Bob updated this Creative (Version 2)')).toBeVisible();
  await expect(ann.getByLabel('Body')).toHaveValue('Ann draft body');
  await expect(ann.getByLabel('Headline')).toHaveValue('Ann headline');

  await ann.getByRole('button', { name: 'Save' }).click();
  await expect(ann.getByRole('alert')).toContainText('Bob saved a newer version');
  await ann.getByRole('button', { name: 'Keep my edits' }).click();

  await expect(ann.getByText('Version 3, last saved by Ann')).toBeVisible();
  await expect(ann.getByLabel('Body')).toHaveValue('Ann draft body');
  await expect(ann.getByLabel('Headline')).toHaveValue('Bob headline');
  await expect(bob.getByLabel('Body')).toHaveValue('Ann draft body');
  await expect(bob.getByText('Version 3, last saved by Ann')).toBeVisible();

  await bob.close();
  await expect(ann.getByText('Open now: Ann')).toBeVisible();

  await ann.context().close();
  await bob.context().close();
});

test('a Viewer cannot create or edit, but sees live updates and Presence', async ({ browser, request }) => {
  const annAccount = await register(request, 'Ann');
  const veraAccount = await register(request, 'Vera');
  await setRole(request, veraAccount, 'Viewer');

  const ann = await openAs(browser, annAccount);
  await ann.getByRole('link', { name: 'New Creative' }).click();
  await ann.getByLabel('Name').fill(`Viewer demo ${Date.now()}`);
  await ann.getByLabel('Headline').fill('First headline');
  await ann.getByLabel('Body').fill('First body');
  await ann.getByLabel('Call to action').fill('First cta');
  await ann.getByRole('button', { name: 'Save' }).click();
  await expect(ann.getByText('Version 1, last saved by Ann')).toBeVisible();

  const viewer = await openAs(browser, veraAccount);
  await expect(viewer.getByRole('link', { name: 'New Creative' })).toHaveCount(0);
  await viewer.goto('/creatives/new');
  await expect(viewer).toHaveURL(/\/creatives$/);

  await viewer.goto(ann.url());
  await expect(viewer.getByText('This Creative is read-only for your role')).toBeVisible();
  await expect(viewer.getByLabel('Headline')).toBeDisabled();
  await expect(viewer.getByRole('button', { name: 'Save' })).toHaveCount(0);
  await expect(viewer.getByText('Open now: Ann, Vera')).toBeVisible();
  await expect(ann.getByText('Open now: Ann, Vera')).toBeVisible();

  await ann.getByLabel('Headline').fill('Second headline');
  await ann.getByRole('button', { name: 'Save' }).click();
  await expect(viewer.getByText('Version 2, last saved by Ann')).toBeVisible();
  await expect(viewer.getByLabel('Headline')).toHaveValue('Second headline');

  await ann.context().close();
  await viewer.context().close();
});
