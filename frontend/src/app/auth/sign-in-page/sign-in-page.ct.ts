import { expect, test } from '@sand4rt/experimental-ct-angular';
import { answer, reply } from '../stub-auth-api';
import { fakeAccessToken } from '../../core/session.testing';
import { SignInPage } from './sign-in-page';

const token = (name = 'Ada') => ({ accessToken: fakeAccessToken(name), expiresIn: 900 });

test('asks for an email and a password, and cannot submit until both are filled', async ({ mount }) => {
  const component = await mount(SignInPage);
  const submit = component.getByRole('button', { name: 'Sign in' });

  await expect(component.getByLabel('Email')).toBeVisible();
  await expect(component.getByLabel('Password')).toHaveAttribute('type', 'password');
  await expect(submit).toBeDisabled();

  await component.getByLabel('Email').fill('ada@example.com');
  await expect(submit).toBeDisabled();
  await component.getByLabel('Password').fill('a long enough password');
  await expect(submit).toBeEnabled();
});

test('sends the credentials and opens the list after a successful sign-in', async ({ mount, page }) => {
  let body: unknown;
  await answer(page, 'sign-in', (route) => {
    body = route.request().postDataJSON();
    return reply(route, 200, token());
  });

  const component = await mount(SignInPage);
  await component.getByLabel('Email').fill('ada@example.com');
  await component.getByLabel('Password').fill('a long enough password');
  await component.getByRole('button', { name: 'Sign in' }).click();

  await expect.poll(() => page.evaluate(() => window.__pwRouter?.url)).toBe('/creatives');
  expect(body).toEqual({ email: 'ada@example.com', password: 'a long enough password' });
});

test('returns to the page the Collaborator wanted', async ({ mount, page }) => {
  await answer(page, 'sign-in', (route) => reply(route, 200, token()));
  const component = await mount(SignInPage);
  await page.evaluate(() => window.__pwRouter?.navigateByUrl('/sign-in?returnUrl=%2Fcreatives%2F4'));

  await component.getByLabel('Email').fill('ada@example.com');
  await component.getByLabel('Password').fill('a long enough password');
  await component.getByRole('button', { name: 'Sign in' }).click();

  await expect.poll(() => page.evaluate(() => window.__pwRouter?.url)).toBe('/creatives/4');
});

test('ignores a return address that leaves the app', async ({ mount, page }) => {
  await answer(page, 'sign-in', (route) => reply(route, 200, token()));
  const component = await mount(SignInPage);
  await page.evaluate(() => window.__pwRouter?.navigateByUrl('/sign-in?returnUrl=%2F%2Fevil.example'));

  await component.getByLabel('Email').fill('ada@example.com');
  await component.getByLabel('Password').fill('a long enough password');
  await component.getByRole('button', { name: 'Sign in' }).click();

  await expect.poll(() => page.evaluate(() => window.__pwRouter?.url)).toBe('/creatives');
});

test('shows the one generic message for a wrong email or password and stays on the form', async ({ mount, page }) => {
  await answer(page, 'sign-in', (route) =>
    reply(route, 401, { title: 'Unauthorized', detail: 'Invalid email or password.' }),
  );

  const component = await mount(SignInPage);
  await component.getByLabel('Email').fill('ada@example.com');
  await component.getByLabel('Password').fill('wrong');
  await component.getByRole('button', { name: 'Sign in' }).click();

  await expect(component.getByRole('alert')).toHaveText('Invalid email or password.');
  await expect(component.getByLabel('Email')).toHaveValue('ada@example.com');
});

test('tells the Collaborator when the account is locked', async ({ mount, page }) => {
  const detail = 'This account is locked after too many failed sign-ins. Please try again in 15 minutes.';
  await answer(page, 'sign-in', (route) => reply(route, 423, { title: 'Locked', detail }));

  const component = await mount(SignInPage);
  await component.getByLabel('Email').fill('ada@example.com');
  await component.getByLabel('Password').fill('wrong');
  await component.getByRole('button', { name: 'Sign in' }).click();

  await expect(component.getByRole('alert')).toHaveText(detail);
});

test('offers registration', async ({ mount }) => {
  const component = await mount(SignInPage);

  await expect(component.getByRole('link', { name: 'Create an account' })).toHaveAttribute('href', '/register');
});
