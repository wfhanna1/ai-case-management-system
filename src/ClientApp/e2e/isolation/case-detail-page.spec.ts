import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { mockGetCase } from '../helpers/api-mocks';
import { createCaseDetailDto, createDocumentDto } from '../fixtures/mock-data';

workerTest.describe('CaseDetailPage', () => {
  workerTest('renders case info and documents', async ({ workerPage: page }) => {
    const doc1 = createDocumentDto({ originalFileName: 'intake-a.pdf', status: 'PendingReview' });
    const doc2 = createDocumentDto({ originalFileName: 'intake-b.pdf', status: 'Finalized' });
    const detail = createCaseDetailDto({
      subjectName: 'Alice Johnson',
      documents: [doc1, doc2],
    });
    await mockGetCase(page, detail);

    await page.goto(`/cases/${detail.id}`);

    await expect(page.getByTestId('case-subject')).toContainText('Alice Johnson');
    await expect(page.getByTestId('case-created')).toBeVisible();
    await expect(page.getByTestId('case-updated')).toBeVisible();
    await expect(page.getByText('intake-a.pdf')).toBeVisible();
    await expect(page.getByText('intake-b.pdf')).toBeVisible();
    await expect(page.getByText('Documents (2)')).toBeVisible();
  });

  workerTest('shows empty documents state', async ({ workerPage: page }) => {
    const detail = createCaseDetailDto({
      subjectName: 'Empty Case',
      documents: [],
    });
    await mockGetCase(page, detail);

    await page.goto(`/cases/${detail.id}`);

    await expect(page.getByTestId('no-case-docs')).toBeVisible();
    await expect(page.getByTestId('no-case-docs')).toHaveText('No documents in this case');
  });

  workerTest('back button navigates to cases list', async ({ workerPage: page }) => {
    const detail = createCaseDetailDto({ subjectName: 'Test Case' });
    await mockGetCase(page, detail);

    // Mock cases list endpoint for navigation target
    await page.route('**/api/cases', route => {
      if (route.request().method() === 'GET') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ data: { items: [], totalCount: 0, page: 1, pageSize: 20 }, error: null }),
        });
      }
      return route.continue();
    });

    await page.goto(`/cases/${detail.id}`);
    await page.getByTestId('back-btn').click();

    await expect(page).toHaveURL(/\/cases$/);
  });

  workerTest('shows error on API failure', async ({ workerPage: page }) => {
    await page.route('**/api/cases/*', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ data: null, error: { code: 'SERVER_ERROR', message: 'Failed to load' } }),
      })
    );

    await page.goto('/cases/00000000-0000-0000-0000-000000000099');

    await expect(page.getByTestId('case-error')).toBeVisible();
  });
});
