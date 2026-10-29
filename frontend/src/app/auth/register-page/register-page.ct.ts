import { expect, test } from '@sand4rt/experimental-ct-angular';
import type { Locator } from 'playwright/test';
import { fakeAccessToken } from '../../core/session.testing';
import { answer, reply } from '../stub-auth-api';
import { RegisterPage } from './register-page';

async function fill(component: Locator, password = 'a long enough password') {
  await component.getByLabel('Display name').fill('Ada Lovelace');
  await component.getByLabel('Email').fill('ada@example.com');
  await component.getByLabel('Password').fill(password);
}

test('asks for a Display name, an email and a password, and says how long the password must be', async ({ mount }) => {
  const component = await mount(RegisterPage);
  const submit = component.getByRole('button', { name: 'Create account' });

  await expect(component.getByLabel('Password')).toHaveAttribute('type', 'password');
  await expect(component).toContainText('at least 12 characters');
  await expect(submit).toBeDisabled();

  await fill(component);
  await expect(submit).toBeEnabled();
});

test('registers, signs in because register does not, and opens the list', async ({ mount, page }) => {
  let registered: unknown;
  let loggedIn: unknown;
  await answer(page, 'register', (route) => {
    registered = route.request().postDataJSON();
    return reply(route, 201, { id: '1', email: 'ada@example.com', displayName: 'Ada Lovelace' });
  });
  await answer(page, 'sign-in', (route) => {
    loggedIn = route.request().postDataJSON();
    return reply(route, 200, { accessToken: fakeAccessToken('Ada Lovelace'), expiresIn: 900 });
  });

  const component = await mount(RegisterPage);
  await fill(component);
  await component.getByRole('button', { name: 'Create account' }).click();

  await expect.poll(() => page.evaluate(() => window.__pwRouter?.url)).toBe('/creatives');
  expect(registered).toEqual({
    email: 'ada@example.com',
    password: 'a long enough password',
    displayName: 'Ada Lovelace',
  });
  expect(loggedIn).toEqual({ email: 'ada@example.com', password: 'a long enough password' });
});

test('shows the server messages next to the fields that need fixing', async ({ mount, page }) => {
  await answer(page, 'register', (route) =>
    reply(route, 400, {
      title: 'One or more validation errors occurred.',
      errors: {
        password: ['Passwords must be at least 12 characters.'],
        email: ["Email 'ada@example.com' is already taken."],
      },
    }),
  );

  const component = await mount(RegisterPage);
  await fill(component, 'short');
  await component.getByRole('button', { name: 'Create account' }).click();

  await expect(component).toContainText('Passwords must be at least 12 characters.');
  await expect(component).toContainText("Email 'ada@example.com' is already taken.");
  await expect(component.getByLabel('Display name')).toHaveValue('Ada Lovelace');
});

test('says so when the server cannot be reached', async ({ mount, page }) => {
  await page.route('http://localhost:5114/api/auth/register', (route) => route.abort());

  const component = await mount(RegisterPage);
  await fill(component);
  await component.getByRole('button', { name: 'Create account' }).click();

  await expect(component.getByRole('alert')).toContainText('Cannot reach the server');
});

test('links back to sign in', async ({ mount }) => {
  const component = await mount(RegisterPage);

  await expect(component.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/sign-in');
});
