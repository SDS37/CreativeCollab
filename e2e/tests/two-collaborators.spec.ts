import { expect, test, type Page } from '@playwright/test';

async function signIn(page: Page, name: string): Promise<void> {
  await page.goto('/');
  await page.getByLabel('Your display name').fill(name);
  await page.getByRole('button', { name: 'Continue' }).click();
  await expect(page.getByRole('heading', { name: 'Creatives' })).toBeVisible();
}

test('two Collaborators keep a dirty Draft, resolve the Conflict, and drop Presence on leave', async ({
  browser,
}) => {
  const name = `Sale ${Date.now()}`;
  const annContext = await browser.newContext();
  const bobContext = await browser.newContext();
  const ann = await annContext.newPage();
  const bob = await bobContext.newPage();

  await signIn(ann, 'Ann');
  await ann.getByRole('link', { name: 'New Creative' }).click();
  await ann.getByLabel('Name').fill(name);
  await ann.getByLabel('Headline').fill('Ann headline');
  await ann.getByLabel('Body').fill('Ann body');
  await ann.getByLabel('Call to action').fill('Ann cta');
  await ann.getByRole('button', { name: 'Save' }).click();

  await expect(ann.getByText('Version 1, last saved by Ann')).toBeVisible();
  await expect(ann.getByText('Open now: Ann')).toBeVisible();

  await signIn(bob, 'Bob');
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

  await annContext.close();
  await bobContext.close();
});
