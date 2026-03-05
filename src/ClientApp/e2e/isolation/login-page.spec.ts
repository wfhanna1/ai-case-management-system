import { test, expect } from '@playwright/test';
import { mockLoginSuccess, mockLoginFailure } from '../helpers/api-mocks';

test.describe('Login page (isolation)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  test('renders login form with email and password fields', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Sign In' })).toBeVisible();
    await expect(page.getByLabel('email address')).toBeVisible();
    await expect(page.getByLabel('password')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign In' })).toBeVisible();
  });

  test('shows validation error for empty email on blur', async ({ page }) => {
    const emailField = page.getByLabel('email address');
    await emailField.focus();
    await emailField.blur();
    await expect(page.getByText('Email is required.')).toBeVisible();
  });

  test('shows validation error for empty password on blur', async ({ page }) => {
    const passwordField = page.getByLabel('password');
    await passwordField.focus();
    await passwordField.blur();
    await expect(page.getByText('Password is required.')).toBeVisible();
  });

  test('shows server error alert on failed login', async ({ page }) => {
    await mockLoginFailure(page, 'INVALID_CREDENTIALS', 'Invalid email or password');
    await page.getByLabel('email address').fill('wrong@test.com');
    await page.getByLabel('password').fill('WrongPass123');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
  });

  test('redirects to dashboard on successful login', async ({ page }) => {
    // Mock a valid JWT with roles claim
    const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({
      sub: '00000000-0000-0000-0000-000000000010',
      email: 'worker@alpha.demo',
      role: 'IntakeWorker',
      tenant_id: 'a1b2c3d4-0000-0000-0000-000000000001',
      exp: Math.floor(Date.now() / 1000) + 3600,
    }));
    const mockToken = `${header}.${payload}.mock-signature`;

    await mockLoginSuccess(page, mockToken);
    await page.getByLabel('email address').fill('worker@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page).toHaveURL(/\/dashboard/);
  });
});
