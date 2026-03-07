import { expect } from '@playwright/test';
import { reviewerTest } from '../fixtures/auth.fixture';
import { createReviewDocumentDto, createExtractedFieldDto, apiOk } from '../fixtures/mock-data';

reviewerTest.describe('Review workflow', () => {
  reviewerTest('finalize changes status to Finalized', async ({ reviewerPage: page }) => {
    const docId = '22222222-2222-2222-2222-222222222222';
    let callCount = 0;

    // Mock review endpoint: first call returns InReview, after finalize returns Finalized
    await page.route(`**/api/reviews/${docId}/similar-cases`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({ items: [] })) })
    );
    await page.route(`**/api/reviews/${docId}`, route => {
      const url = route.request().url();
      if (url.includes('/start') || url.includes('/correct-field') || url.includes('/finalize') ||
          url.includes('/audit') || url.includes('/similar-cases')) {
        return route.continue();
      }
      callCount++;
      const status = callCount <= 2 ? 'InReview' : 'Finalized';
      const doc = createReviewDocumentDto({
        id: docId,
        status,
        reviewedBy: status === 'Finalized' ? '00000000-0000-0000-0000-000000000020' : null,
        reviewedAt: status === 'Finalized' ? '2026-03-01T11:00:00Z' : null,
      });
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(doc)) });
    });
    await page.route(`**/api/reviews/${docId}/finalize`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({})) })
    );

    // Mock pending reviews endpoint for post-finalize navigation
    await page.route('**/api/reviews/pending**', route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({ items: [], totalCount: 0 })) })
    );

    await page.goto(`/reviews/${docId}`);
    await expect(page.getByTestId('review-status')).toHaveText('InReview');

    // Click finalize
    await page.getByTestId('finalize-btn').click();
    await page.getByTestId('confirm-finalize-btn').click();

    // After finalize, app navigates to review queue
    await page.waitForURL('**/reviews', { timeout: 10000 });
    await expect(page.getByText('Review Queue')).toBeVisible();
  });

  reviewerTest('correct field saves corrected value', async ({ reviewerPage: page }) => {
    const docId = '33333333-3333-3333-3333-333333333333';
    let corrected = false;

    await page.route(`**/api/reviews/${docId}/similar-cases`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({ items: [] })) })
    );
    await page.route(`**/api/reviews/${docId}`, route => {
      const url = route.request().url();
      if (url.includes('/start') || url.includes('/correct-field') || url.includes('/finalize') ||
          url.includes('/audit') || url.includes('/similar-cases')) {
        return route.continue();
      }
      const fields = [
        createExtractedFieldDto({
          name: 'PatientName',
          value: 'John Doe',
          confidence: 0.95,
          correctedValue: corrected ? 'Jane Doe' : null,
        }),
        createExtractedFieldDto({ name: 'DateOfBirth', value: '1990-01-15', confidence: 0.82 }),
      ];
      const doc = createReviewDocumentDto({ id: docId, status: 'InReview', extractedFields: fields });
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(doc)) });
    });
    await page.route(`**/api/reviews/${docId}/correct-field`, route => {
      corrected = true;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({})) });
    });

    await page.goto(`/reviews/${docId}`);

    // Click edit on PatientName
    await page.getByTestId('edit-btn-PatientName').click();
    const input = page.getByTestId('edit-input-PatientName').locator('input');
    await input.clear();
    await input.fill('Jane Doe');
    await page.getByTestId('save-edit-PatientName').click();

    // After save, corrected value should appear
    await expect(page.getByTestId('field-row-PatientName')).toContainText('Jane Doe', { timeout: 10000 });
  });
});
