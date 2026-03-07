import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { createRecentActivityDto, apiOk } from '../fixtures/mock-data';
import { mockGetRecentActivity } from '../helpers/api-mocks';

workerTest.describe('Dashboard recent activity (isolation)', () => {
  workerTest.beforeEach(async ({ workerPage }) => {
    // Mock stats so dashboard loads cleanly
    await workerPage.route('**/api/documents/stats', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(apiOk({
          totalCases: 5,
          pendingReview: 1,
          processedToday: 2,
          averageProcessingTime: '3m',
        })),
      })
    );
  });

  workerTest('shows recent activity entries', async ({ workerPage }) => {
    const activities = [
      createRecentActivityDto({ action: 'ReviewFinalized', timestamp: '2026-03-01T12:00:00Z' }),
      createRecentActivityDto({ action: 'FieldCorrected', fieldName: 'PatientName', timestamp: '2026-03-01T11:50:00Z' }),
      createRecentActivityDto({ action: 'ExtractionCompleted', timestamp: '2026-03-01T11:00:00Z' }),
    ];
    await mockGetRecentActivity(workerPage, activities);
    await workerPage.goto('/dashboard');

    await expect(workerPage.getByTestId('activity-list')).toBeVisible();
    await expect(workerPage.getByText('ReviewFinalized')).toBeVisible();
    await expect(workerPage.getByText('FieldCorrected')).toBeVisible();
    await expect(workerPage.getByText('ExtractionCompleted')).toBeVisible();
    await expect(workerPage.getByText('PatientName')).toBeVisible();
  });

  workerTest('shows empty state when no activity', async ({ workerPage }) => {
    await mockGetRecentActivity(workerPage, []);
    await workerPage.goto('/dashboard');

    await expect(workerPage.getByText('No recent activity yet.')).toBeVisible();
  });

  workerTest('shows activity section even when API fails', async ({ workerPage }) => {
    await workerPage.route('**/api/documents/recent-activity*', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ data: null, error: { code: 'DB_ERROR', message: 'timeout' } }),
      })
    );
    await workerPage.goto('/dashboard');

    await expect(workerPage.getByTestId('recent-activity')).toBeVisible();
  });
});
