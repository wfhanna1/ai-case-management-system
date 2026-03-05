import { expect } from '@playwright/test';
import { reviewerTest } from '../fixtures/auth.fixture';
import { createReviewDocumentDto } from '../fixtures/mock-data';
import { mockGetReviewQueue, mockApiError } from '../helpers/api-mocks';

reviewerTest.describe('Review queue page (isolation)', () => {
  reviewerTest('renders review queue with documents', async ({ reviewerPage }) => {
    const docs = [
      createReviewDocumentDto({ originalFileName: 'form-1.pdf', status: 'PendingReview' }),
      createReviewDocumentDto({ originalFileName: 'form-2.pdf', status: 'InReview' }),
    ];
    await mockGetReviewQueue(reviewerPage, docs);
    await reviewerPage.goto('/reviews');

    await expect(reviewerPage.getByRole('heading', { name: 'Review Queue' })).toBeVisible();
    await expect(reviewerPage.getByText('form-1.pdf')).toBeVisible();
    await expect(reviewerPage.getByText('form-2.pdf')).toBeVisible();
  });

  reviewerTest('shows table column headers', async ({ reviewerPage }) => {
    await mockGetReviewQueue(reviewerPage, []);
    await reviewerPage.goto('/reviews');

    await expect(reviewerPage.getByRole('columnheader', { name: 'File Name' })).toBeVisible();
    await expect(reviewerPage.getByRole('columnheader', { name: 'Status' })).toBeVisible();
    await expect(reviewerPage.getByRole('columnheader', { name: 'Submitted At' })).toBeVisible();
    await expect(reviewerPage.getByRole('columnheader', { name: 'Processed At' })).toBeVisible();
    await expect(reviewerPage.getByRole('columnheader', { name: 'Action' })).toBeVisible();
  });

  reviewerTest('shows pending badge count', async ({ reviewerPage }) => {
    const docs = [
      createReviewDocumentDto({ status: 'PendingReview' }),
      createReviewDocumentDto({ status: 'PendingReview' }),
    ];
    await mockGetReviewQueue(reviewerPage, docs);
    await reviewerPage.goto('/reviews');

    const badge = reviewerPage.getByTestId('pending-badge');
    await expect(badge).toBeVisible();
  });

  reviewerTest('shows empty queue message', async ({ reviewerPage }) => {
    await mockGetReviewQueue(reviewerPage, []);
    await reviewerPage.goto('/reviews');

    await expect(reviewerPage.getByText('No documents pending review')).toBeVisible();
  });

  reviewerTest('handles API error', async ({ reviewerPage }) => {
    await mockApiError(reviewerPage, '**/api/reviews/pending', 'SERVER_ERROR', 'Connection failed');
    await reviewerPage.goto('/reviews');

    await expect(reviewerPage.getByRole('alert')).toBeVisible();
  });

  reviewerTest('review button navigates to detail page', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'PendingReview' });
    await mockGetReviewQueue(reviewerPage, [doc]);
    await reviewerPage.goto('/reviews');

    await reviewerPage.getByTestId(`review-btn-${doc.id}`).click();
    await expect(reviewerPage).toHaveURL(new RegExp(`/reviews/${doc.id}`));
  });
});
