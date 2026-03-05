import { expect } from '@playwright/test';
import { reviewerTest } from '../fixtures/auth.fixture';
import { createDocumentDto } from '../fixtures/mock-data';
import { mockGetDocuments } from '../helpers/api-mocks';

reviewerTest.describe('Documents page review button (isolation)', () => {
  reviewerTest('shows Review button for PendingReview documents', async ({ reviewerPage }) => {
    const docs = [
      createDocumentDto({ id: 'aaa-001', originalFileName: 'pending.pdf', status: 'PendingReview' }),
      createDocumentDto({ id: 'aaa-002', originalFileName: 'completed.pdf', status: 'Completed' }),
      createDocumentDto({ id: 'aaa-003', originalFileName: 'in-review.pdf', status: 'InReview' }),
    ];
    await mockGetDocuments(reviewerPage, docs);
    await reviewerPage.goto('/documents');

    await expect(reviewerPage.getByRole('columnheader', { name: 'Actions' })).toBeVisible();
    await expect(reviewerPage.getByTestId('review-btn-aaa-001')).toBeVisible();
    await expect(reviewerPage.getByTestId('review-btn-aaa-002')).toHaveCount(0);
    await expect(reviewerPage.getByTestId('review-btn-aaa-003')).toHaveCount(0);
  });

  reviewerTest('review button navigates to review detail page', async ({ reviewerPage }) => {
    const docs = [
      createDocumentDto({ id: 'nav-001', originalFileName: 'navigate.pdf', status: 'PendingReview' }),
    ];
    await mockGetDocuments(reviewerPage, docs);
    await reviewerPage.goto('/documents');

    await reviewerPage.getByTestId('review-btn-nav-001').click();
    await expect(reviewerPage).toHaveURL(/\/reviews\/nav-001/);
  });

  reviewerTest('does not show Review button for non-PendingReview documents', async ({ reviewerPage }) => {
    const docs = [
      createDocumentDto({ originalFileName: 'completed.pdf', status: 'Completed' }),
      createDocumentDto({ originalFileName: 'in-review.pdf', status: 'InReview' }),
      createDocumentDto({ originalFileName: 'submitted.pdf', status: 'Submitted' }),
    ];
    await mockGetDocuments(reviewerPage, docs);
    await reviewerPage.goto('/documents');

    await expect(reviewerPage.getByRole('columnheader', { name: 'Actions' })).toBeVisible();
    const table = reviewerPage.locator('table');
    await expect(table.getByRole('button', { name: 'Review' })).toHaveCount(0);
  });
});
