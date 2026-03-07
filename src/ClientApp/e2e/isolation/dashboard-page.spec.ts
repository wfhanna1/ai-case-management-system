import { test, expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { apiOk } from '../fixtures/mock-data';
import { mockGetRecentActivity } from '../helpers/api-mocks';

test.describe('Dashboard page (isolation)', () => {
  test('redirects to login when not authenticated', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/login/);
  });

  workerTest('renders dashboard heading for authenticated user', async ({ workerPage }) => {
    await mockGetRecentActivity(workerPage, []);
    await workerPage.goto('/dashboard');
    await expect(workerPage.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
  });

  workerTest('shows stat cards', async ({ workerPage }) => {
    await mockGetRecentActivity(workerPage, []);
    await workerPage.route('**/api/documents/stats', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(apiOk({
          totalCases: 10,
          pendingReview: 2,
          processedToday: 1,
          averageProcessingTime: '5m',
        })),
      })
    );
    await workerPage.goto('/dashboard');
    await expect(workerPage.getByText('Total Cases')).toBeVisible();
    await expect(workerPage.getByText('Pending Review')).toBeVisible();
    await expect(workerPage.getByText('Processed Today')).toBeVisible();
  });
});
