import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { mockSearchDocuments } from '../helpers/api-mocks';
import {
  createDocumentDto,
  createSearchDocumentsResultDto,
} from '../fixtures/mock-data';

workerTest.describe('SearchPage', () => {
  workerTest('renders search filters', async ({ workerPage: page }) => {
    await page.goto('/search');

    await expect(page.getByRole('heading', { name: 'Search Documents' })).toBeVisible();
    await expect(page.getByTestId('search-filename')).toBeVisible();
    await expect(page.getByTestId('search-status')).toBeVisible();
    await expect(page.getByTestId('search-fieldvalue')).toBeVisible();
    await expect(page.getByTestId('search-from')).toBeVisible();
    await expect(page.getByTestId('search-to')).toBeVisible();
    await expect(page.getByTestId('search-btn')).toBeVisible();
    await expect(page.getByTestId('clear-btn')).toBeVisible();
  });

  workerTest('shows prompt message before search', async ({ workerPage: page }) => {
    await page.goto('/search');

    await expect(page.getByTestId('search-prompt')).toBeVisible();
    await expect(page.getByTestId('search-prompt')).toHaveText('Use the filters above and click Search');
  });

  workerTest('displays search results', async ({ workerPage: page }) => {
    const docs = [
      createDocumentDto({ originalFileName: 'intake-form.pdf', status: 'PendingReview' }),
      createDocumentDto({ originalFileName: 'referral.pdf', status: 'Submitted' }),
    ];
    const result = createSearchDocumentsResultDto(docs, 2);
    await mockSearchDocuments(page, result);

    await page.goto('/search');
    await page.getByTestId('search-btn').click();

    await expect(page.getByText('intake-form.pdf')).toBeVisible();
    await expect(page.getByText('referral.pdf')).toBeVisible();
  });

  workerTest('shows empty state after search with no results', async ({ workerPage: page }) => {
    const result = createSearchDocumentsResultDto([], 0);
    await mockSearchDocuments(page, result);

    await page.goto('/search');
    await page.getByTestId('search-btn').click();

    await expect(page.getByTestId('no-results')).toBeVisible();
    await expect(page.getByTestId('no-results')).toHaveText('No documents found');
  });

  workerTest('shows pagination for large result sets', async ({ workerPage: page }) => {
    const docs = Array.from({ length: 5 }, (_, i) =>
      createDocumentDto({ originalFileName: `doc-${i}.pdf` })
    );
    const result = createSearchDocumentsResultDto(docs, 50, 1, 5);
    await mockSearchDocuments(page, result);

    await page.goto('/search');
    await page.getByTestId('search-btn').click();

    await expect(page.getByTestId('search-pagination')).toBeVisible();
  });

  workerTest('shows error alert on API failure', async ({ workerPage: page }) => {
    await page.route('**/api/documents/search*', route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ data: null, error: { code: 'SERVER_ERROR', message: 'Internal error' } }),
      })
    );

    await page.goto('/search');
    await page.getByTestId('search-btn').click();

    await expect(page.getByTestId('search-error')).toBeVisible();
  });
});
