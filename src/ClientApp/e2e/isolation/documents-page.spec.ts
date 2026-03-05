import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { createDocumentDto } from '../fixtures/mock-data';
import { mockGetDocuments, mockApiError } from '../helpers/api-mocks';

workerTest.describe('Documents page (isolation)', () => {
  workerTest('renders document table with mocked data', async ({ workerPage }) => {
    const docs = [
      createDocumentDto({ originalFileName: 'intake-1.pdf', status: 'Submitted' }),
      createDocumentDto({ originalFileName: 'intake-2.pdf', status: 'Completed', processedAt: '2026-03-01T11:00:00Z' }),
    ];
    await mockGetDocuments(workerPage, docs);
    await workerPage.goto('/documents');

    await expect(workerPage.getByRole('heading', { name: 'Documents' })).toBeVisible();
    await expect(workerPage.getByText('intake-1.pdf')).toBeVisible();
    await expect(workerPage.getByText('intake-2.pdf')).toBeVisible();
  });

  workerTest('shows correct status chips', async ({ workerPage }) => {
    const docs = [
      createDocumentDto({ originalFileName: 'submitted.pdf', status: 'Submitted' }),
      createDocumentDto({ originalFileName: 'processing.pdf', status: 'Processing' }),
      createDocumentDto({ originalFileName: 'completed.pdf', status: 'Completed' }),
      createDocumentDto({ originalFileName: 'failed.pdf', status: 'Failed' }),
    ];
    await mockGetDocuments(workerPage, docs);
    await workerPage.goto('/documents');

    // Use Chip locators scoped to each row to avoid matching file names
    const submittedRow = workerPage.locator('tr', { hasText: 'submitted.pdf' });
    await expect(submittedRow.locator('.MuiChip-label')).toHaveText('Submitted');

    const processingRow = workerPage.locator('tr', { hasText: 'processing.pdf' });
    await expect(processingRow.locator('.MuiChip-label')).toHaveText('Processing');

    const completedRow = workerPage.locator('tr', { hasText: 'completed.pdf' });
    await expect(completedRow.locator('.MuiChip-label')).toHaveText('Completed');

    const failedRow = workerPage.locator('tr', { hasText: 'failed.pdf' });
    await expect(failedRow.locator('.MuiChip-label')).toHaveText('Failed');
  });

  workerTest('shows table headers', async ({ workerPage }) => {
    await mockGetDocuments(workerPage, []);
    await workerPage.goto('/documents');

    await expect(workerPage.getByRole('columnheader', { name: 'File Name' })).toBeVisible();
    await expect(workerPage.getByRole('columnheader', { name: 'Status' })).toBeVisible();
    await expect(workerPage.getByRole('columnheader', { name: 'Submitted At' })).toBeVisible();
    await expect(workerPage.getByRole('columnheader', { name: 'Processed At' })).toBeVisible();
  });

  workerTest('handles empty state', async ({ workerPage }) => {
    await mockGetDocuments(workerPage, []);
    await workerPage.goto('/documents');

    await expect(workerPage.getByRole('heading', { name: 'Documents' })).toBeVisible();
  });

  workerTest('handles API error', async ({ workerPage }) => {
    await mockApiError(workerPage, '**/api/documents', 'SERVER_ERROR', 'Internal server error');
    await workerPage.goto('/documents');

    await expect(workerPage.getByRole('alert')).toBeVisible();
  });
});
