import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { apiOk } from '../fixtures/mock-data';

workerTest.describe('Dashboard stats', () => {
  workerTest('displays stats from API', async ({ workerPage: page }) => {
    await page.route('**/api/documents/stats', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(apiOk({
          totalCases: 42,
          pendingReview: 7,
          processedToday: 3,
          averageProcessingTime: '1h 15m',
        })),
      })
    );

    await page.goto('/dashboard');

    await expect(page.getByTestId('stat-total-cases')).toHaveText('42');
    await expect(page.getByTestId('stat-pending-review')).toHaveText('7');
    await expect(page.getByTestId('stat-processed-today')).toHaveText('3');
    await expect(page.getByTestId('stat-avg-processing-time')).toHaveText('1h 15m');
  });

  workerTest('shows error alert on API failure', async ({ workerPage: page }) => {
    await page.route('**/api/documents/stats', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ data: null, error: { code: 'DB_ERROR', message: 'timeout' } }),
      })
    );

    await page.goto('/dashboard');

    await expect(page.getByText('Failed to load dashboard stats')).toBeVisible({ timeout: 10000 });
  });
});
