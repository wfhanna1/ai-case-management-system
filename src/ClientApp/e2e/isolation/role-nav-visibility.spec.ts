import { expect } from '@playwright/test';
import { reviewerTest, workerTest, adminTest } from '../fixtures/auth.fixture';
import { apiOk } from '../fixtures/mock-data';

async function mockDashboardApis(page: import('@playwright/test').Page) {
  await page.route('**/api/documents/stats', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(apiOk({
        totalCases: 0,
        pendingReview: 0,
        processedToday: 0,
        averageProcessingTime: '--',
      })),
    })
  );
  await page.route('**/api/documents/recent-activity*', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(apiOk([])),
    })
  );
}

reviewerTest.describe('Role-based navigation visibility (isolation)', () => {
  reviewerTest('reviewer does not see Upload nav item', async ({ reviewerPage: page }) => {
    await mockDashboardApis(page);
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    await page.getByTestId('mobile-menu-btn').click();
    await expect(page.getByTestId('mobile-drawer')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-upload')).toHaveCount(0);
    await expect(page.getByTestId('mobile-nav-reviews')).toBeVisible();
  });
});

workerTest.describe('Role-based navigation visibility (isolation)', () => {
  workerTest('worker does not see Reviews nav item', async ({ workerPage: page }) => {
    await mockDashboardApis(page);
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    await page.getByTestId('mobile-menu-btn').click();
    await expect(page.getByTestId('mobile-drawer')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-reviews')).toHaveCount(0);
    await expect(page.getByTestId('mobile-nav-upload')).toBeVisible();
  });
});

adminTest.describe('Role-based navigation visibility (isolation)', () => {
  adminTest('admin sees both Upload and Reviews nav items', async ({ adminPage: page }) => {
    await mockDashboardApis(page);
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    await page.getByTestId('mobile-menu-btn').click();
    await expect(page.getByTestId('mobile-drawer')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-upload')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-reviews')).toBeVisible();
  });
});

reviewerTest.describe('Role-based route access (isolation)', () => {
  reviewerTest('reviewer is redirected from /upload to /dashboard', async ({ reviewerPage: page }) => {
    await mockDashboardApis(page);
    await page.goto('/upload');

    await expect(page).toHaveURL(/\/dashboard/, { timeout: 5000 });
  });
});
