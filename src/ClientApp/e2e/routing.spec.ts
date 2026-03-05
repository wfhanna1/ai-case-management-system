import { test, expect } from '@playwright/test';

test.describe('Root redirect', () => {
  test('unauthenticated user at / is redirected to /login', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL('/login');
  });

  test('authenticated user at / is redirected to /dashboard', async ({ page }) => {
    // Login first
    await page.goto('/login');
    await page.getByLabel('email address').fill('admin@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL('/dashboard');

    // Navigate to root
    await page.goto('/');
    await expect(page).toHaveURL('/dashboard');
  });
});
