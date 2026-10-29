import { expect, type APIRequestContext, type Page } from '@playwright/test';

export const API = 'http://localhost:5114/api';

/**
 * The first Admin, seeded by the API from the config the Playwright web server passes it. The password is
 * generated per run in playwright.config.ts and handed over through the environment, so none is committed.
 */
export const ADMIN = {
  email: 'e2e-admin@example.test',
  displayName: 'E2E Admin',
  get password(): string {
    const password = process.env.E2E_ADMIN_PASSWORD;
    if (!password) throw new Error('E2E_ADMIN_PASSWORD is set by playwright.config.ts; run the suite through Playwright.');
    return password;
  },
};

const PASSWORD = 'a long e2e passphrase';

export interface Account {
  id: string;
  email: string;
  password: string;
  displayName: string;
}

/** Registers a new account with an email and a Display name no other test uses (names are unique). Register does not sign in. */
export async function register(request: APIRequestContext, firstName: string): Promise<Account> {
  const suffix = Math.random().toString(36).slice(2, 8);
  const displayName = `${firstName} ${suffix}`;
  const email = `${firstName.toLowerCase()}-${Date.now()}-${suffix}@example.test`;
  const response = await request.post(`${API}/auth/register`, { data: { email, password: PASSWORD, displayName } });
  expect(response.status()).toBe(201);
  const { id } = (await response.json()) as { id: string };
  return { id, email, password: PASSWORD, displayName };
}

/** Changes a role through the real Admin endpoint, signed in as the seeded Admin. */
export async function setRole(
  request: APIRequestContext,
  account: Account,
  role: 'Viewer' | 'Editor' | 'Admin',
): Promise<void> {
  const response = await request.post(`${API}/auth/sign-in`, { data: { email: ADMIN.email, password: ADMIN.password } });
  expect(response.ok(), 'the seeded Admin can sign in').toBe(true);
  const { accessToken } = (await response.json()) as { accessToken: string };
  const changed = await request.put(`${API}/admin/accounts/${account.id}/role`, {
    data: { role },
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  expect(changed.status()).toBe(204);
}

/** Signs in through the sign-in screen, so the role in the token is the one the account has now. */
export async function signIn(page: Page, account: Account, returnTo = '/'): Promise<void> {
  await page.goto(returnTo);
  await page.getByLabel('Email').fill(account.email);
  await page.getByLabel('Password').fill(account.password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByText(`Signed in as ${account.displayName}`)).toBeVisible();
}
