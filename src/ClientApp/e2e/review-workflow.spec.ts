import { test, expect } from '@playwright/test';

test.describe('Review workflow', () => {
  test.describe('Reviewer access', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/login');
      await page.getByLabel('email address').fill('reviewer@alpha.demo');
      await page.getByLabel('password').fill('Demo123!');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await page.waitForURL(/\/(dashboard|$)/);
    });

    test('reviewer sees Reviews nav link', async ({ page }) => {
      await expect(page.getByRole('button', { name: 'Reviews' })).toBeVisible();
    });

    test('reviewer can navigate to review queue', async ({ page }) => {
      await page.getByRole('button', { name: 'Reviews' }).click();
      await page.waitForURL(/\/reviews/);
      await expect(page.getByRole('heading', { name: 'Review Queue' })).toBeVisible();
    });

    test('review queue shows table columns', async ({ page }) => {
      await page.goto('/reviews');
      await expect(page.getByRole('columnheader', { name: 'File Name' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Status' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Submitted At' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Processed At' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Action' })).toBeVisible();
    });

    test('empty queue shows empty message', async ({ page }) => {
      // Mock the review queue API to return empty results
      await page.route(/\/api\/reviews\/pending/, route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: [], error: null }),
        })
      );

      await page.goto('/reviews');
      await expect(page.getByText('No documents pending review')).toBeVisible();
    });
  });

  test.describe('IntakeWorker access restriction', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/login');
      await page.getByLabel('email address').fill('worker@alpha.demo');
      await page.getByLabel('password').fill('Demo123!');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await page.waitForURL(/\/(dashboard|$)/);
    });

    test('IntakeWorker does not see Reviews nav link', async ({ page }) => {
      await expect(page.getByRole('button', { name: 'Reviews' })).not.toBeVisible();
    });

    test('IntakeWorker redirected from /reviews', async ({ page }) => {
      await page.goto('/reviews');
      await page.waitForURL(/\/dashboard/);
    });
  });

  test.describe('Admin access', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/login');
      await page.getByLabel('email address').fill('admin@alpha.demo');
      await page.getByLabel('password').fill('Demo123!');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await page.waitForURL(/\/(dashboard|$)/);
    });

    test('Admin sees Reviews nav link', async ({ page }) => {
      await expect(page.getByRole('button', { name: 'Reviews' })).toBeVisible();
    });

    test('Admin can access review queue', async ({ page }) => {
      await page.goto('/reviews');
      await expect(page.getByRole('heading', { name: 'Review Queue' })).toBeVisible();
    });
  });
});
