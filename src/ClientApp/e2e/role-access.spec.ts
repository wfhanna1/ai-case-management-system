import { test, expect } from '@playwright/test';

const DEMO_PASSWORD = 'Demo123!';

async function loginAs(page: import('@playwright/test').Page, email: string) {
  await page.goto('/login');
  await page.getByLabel('email address').fill(email);
  await page.getByLabel('password').fill(DEMO_PASSWORD);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await expect(page).toHaveURL('/dashboard', { timeout: 10000 });
}

test.describe('Role-based page access (real API)', () => {
  test.describe('IntakeWorker', () => {
    test.beforeEach(async ({ page }) => {
      await loginAs(page, 'worker@beta.demo');
    });

    test('can access cases page', async ({ page }) => {
      await page.goto('/cases');
      await expect(page.getByRole('heading', { name: /cases/i })).toBeVisible({ timeout: 10000 });
      // Should NOT get a 403 -- this is the #136 regression test
    });

    test('can access upload page', async ({ page }) => {
      await page.goto('/upload');
      await expect(page.getByRole('heading', { name: /upload/i })).toBeVisible({ timeout: 10000 });
    });

    test('can access search page', async ({ page }) => {
      await page.goto('/search');
      await expect(page.getByRole('heading', { name: /search/i })).toBeVisible({ timeout: 10000 });
    });
  });

  test.describe('Reviewer', () => {
    test.beforeEach(async ({ page }) => {
      await loginAs(page, 'reviewer@beta.demo');
    });

    test('can access review queue', async ({ page }) => {
      await page.goto('/reviews');
      await expect(page.getByRole('heading', { name: /review/i })).toBeVisible({ timeout: 10000 });
    });

    test('can access cases page', async ({ page }) => {
      await page.goto('/cases');
      await expect(page.getByRole('heading', { name: /cases/i })).toBeVisible({ timeout: 10000 });
    });

    test('can access search page', async ({ page }) => {
      await page.goto('/search');
      await expect(page.getByRole('heading', { name: /search/i })).toBeVisible({ timeout: 10000 });
    });
  });

  test.describe('Admin', () => {
    test.beforeEach(async ({ page }) => {
      await loginAs(page, 'admin@beta.demo');
    });

    test('can access all pages', async ({ page }) => {
      await page.goto('/upload');
      await expect(page.getByRole('heading', { name: /upload/i })).toBeVisible({ timeout: 10000 });

      await page.goto('/cases');
      await expect(page.getByRole('heading', { name: /cases/i })).toBeVisible({ timeout: 10000 });

      await page.goto('/reviews');
      await expect(page.getByRole('heading', { name: /review/i })).toBeVisible({ timeout: 10000 });

      await page.goto('/search');
      await expect(page.getByRole('heading', { name: /search/i })).toBeVisible({ timeout: 10000 });

      await page.goto('/documents');
      await expect(page.getByRole('heading', { name: /documents/i })).toBeVisible({ timeout: 10000 });
    });
  });
});
