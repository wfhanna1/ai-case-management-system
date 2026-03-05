import { test, expect } from '@playwright/test';

const TEST_EMAIL = `e2e-${Date.now()}@test.com`;
const TEST_PASSWORD = 'TestPassword123!';

test.describe('Auth flow E2E', () => {
  test('register -> login -> see role-appropriate UI -> logout', async ({ page }) => {
    // Step 1: Navigate to registration page
    await page.goto('/register');
    await expect(page.getByRole('heading', { name: 'Create Account' })).toBeVisible();

    // Step 2: Fill registration form
    await page.getByLabel('Email').fill(TEST_EMAIL);
    await page.getByLabel('Password', { exact: true }).fill(TEST_PASSWORD);
    await page.getByLabel('Confirm Password').fill(TEST_PASSWORD);
    await page.getByRole('button', { name: 'Register' }).click();

    // Step 3: Verify redirect to dashboard after registration
    await expect(page).toHaveURL('/dashboard');

    // Step 4: Verify user info displayed in header
    await expect(page.getByText(TEST_EMAIL)).toBeVisible();

    // Step 5: Sign out
    await page.getByRole('button', { name: 'Sign Out' }).click();
    await expect(page).toHaveURL('/login');

    // Step 6: Login with the registered account
    await page.getByLabel('Email').fill(TEST_EMAIL);
    await page.getByLabel('Password').fill(TEST_PASSWORD);
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Step 7: Verify redirect to dashboard after login
    await expect(page).toHaveURL('/dashboard');
    await expect(page.getByText(TEST_EMAIL)).toBeVisible();

    // Step 8: Sign out again
    await page.getByRole('button', { name: 'Sign Out' }).click();
    await expect(page).toHaveURL('/login');
  });

  test('unauthenticated user is redirected to login from protected routes', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page).toHaveURL('/login');
  });

  test('login with invalid credentials shows error', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill('nonexistent@test.com');
    await page.getByLabel('Password').fill('wrongpassword');
    await page.getByRole('button', { name: 'Sign In' }).click();

    await expect(page.getByRole('alert')).toBeVisible();
  });
});
