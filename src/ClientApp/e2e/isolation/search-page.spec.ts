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

  workerTest('clicking search result navigates to document detail page', async ({ workerPage: page }) => {
    const docId = '11111111-1111-1111-1111-111111111111';
    const docs = [
      createDocumentDto({ id: docId, originalFileName: 'test-doc.pdf', status: 'PendingReview' }),
    ];
    const result = createSearchDocumentsResultDto(docs, 1);
    await mockSearchDocuments(page, result);

    // Mock the review endpoint so ReviewDetailPage loads after navigation
    await page.route(`**/api/reviews/${docId}/**`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: null, error: null }) })
    );
    await page.route(`**/api/reviews/${docId}`, route =>
      route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ data: { id: docId, originalFileName: 'test-doc.pdf', status: 'PendingReview', submittedAt: '2026-03-01T10:00:00Z', processedAt: null, reviewedBy: null, reviewedAt: null, extractedFields: [] }, error: null }),
      })
    );

    await page.goto('/search');
    await page.getByTestId('search-btn').click();
    await expect(page.getByTestId(`search-result-${docId}`)).toBeVisible();
    await page.getByTestId(`search-result-${docId}`).click();

    await expect(page).toHaveURL(/\/documents\//, { timeout: 5000 });
  });

  workerTest('date search sends end-of-day for To date', async ({ workerPage: page }) => {
    let capturedUrl = '';
    await page.route('**/api/documents/search*', route => {
      capturedUrl = route.request().url();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { items: [], totalCount: 0, page: 1, pageSize: 20 }, error: null }),
      });
    });

    await page.goto('/search');
    await page.getByTestId('search-to').locator('input').fill('2026-03-06');
    await page.getByTestId('search-btn').click();

    await expect(async () => {
      expect(capturedUrl).toBeTruthy();
    }).toPass({ timeout: 3000 });

    // The To date should be end of day, not midnight
    expect(capturedUrl).toContain('to=2026-03-06T23:59:59');
  });

  workerTest('date inputs disallow future dates', async ({ workerPage: page }) => {
    await page.goto('/search');
    const today = new Date().toISOString().split('T')[0];

    const fromMax = await page.getByTestId('search-from').locator('input').getAttribute('max');
    const toMax = await page.getByTestId('search-to').locator('input').getAttribute('max');

    expect(fromMax).toBe(today);
    expect(toMax).toBe(today);
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
