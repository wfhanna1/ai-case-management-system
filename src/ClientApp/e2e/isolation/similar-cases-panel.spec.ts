import { expect } from '@playwright/test';
import { reviewerTest } from '../fixtures/auth.fixture';
import {
  createReviewDocumentDto,
  createSimilarCaseDto,
} from '../fixtures/mock-data';
import {
  mockGetReviewDocument,
  mockGetSimilarCases,
} from '../helpers/api-mocks';

reviewerTest.describe('Similar cases panel (isolation)', () => {
  reviewerTest('shows similar cases panel with results', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const similar = {
      items: [
        createSimilarCaseDto({ score: 0.95, summary: 'Subject: Alice Johnson. Category: ChildWelfare.' }),
        createSimilarCaseDto({ score: 0.82, summary: 'Subject: Bob Martinez. Category: AdultProtective.' }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, similar);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    const panel = reviewerPage.getByTestId('similar-cases-panel');
    await expect(panel).toBeVisible();
    await expect(panel.getByText('Similar Cases')).toBeVisible();
  });

  reviewerTest('shows score badges with correct colors', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const id1 = '11111111-1111-1111-1111-111111111111';
    const id2 = '22222222-2222-2222-2222-222222222222';
    const similar = {
      items: [
        createSimilarCaseDto({ documentId: id1, score: 0.95 }),
        createSimilarCaseDto({ documentId: id2, score: 0.65 }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, similar);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Expand the panel
    await reviewerPage.getByText('Similar Cases').click();

    const badge1 = reviewerPage.getByTestId(`score-badge-${id1}`);
    await expect(badge1).toHaveText('95% match');

    const badge2 = reviewerPage.getByTestId(`score-badge-${id2}`);
    await expect(badge2).toHaveText('65% match');
  });

  reviewerTest('shows empty state when no similar cases', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, { items: [] });
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Wait for the page to load, then expand the panel
    const panel = reviewerPage.getByTestId('similar-cases-panel');
    await expect(panel).toBeVisible();
    await panel.click();

    await expect(reviewerPage.getByTestId('no-similar-cases')).toBeVisible();
  });

  reviewerTest('shows shared fields as chips when present', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const id1 = '33333333-3333-3333-3333-333333333333';
    const similarData = {
      items: [
        {
          documentId: id1,
          score: 0.92,
          summary: 'Subject: Test Patient. Category: ChildWelfare.',
          metadata: { ChildName: 'Test Patient', Age: '8', ReasonForReferral: 'Neglect' },
          sharedFields: { ChildName: 'Test Patient', Age: '8' },
        },
      ],
    };

    // Set up similar-cases mock BEFORE the review doc mock to avoid override issues
    await reviewerPage.route(`**/api/reviews/${doc.id}/similar-cases`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: similarData, error: null }),
      })
    );
    await reviewerPage.route(`**/api/reviews/${doc.id}`, route => {
      if (route.request().url().includes('/similar-cases') ||
          route.request().url().includes('/audit') ||
          route.request().url().includes('/start')) {
        return route.continue();
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: doc, error: null }),
      });
    });

    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Expand similar cases panel
    await reviewerPage.getByText('Similar Cases').click();

    // Wait for similar case card to be visible (accordion expanded)
    await expect(reviewerPage.getByTestId(`similar-case-${id1}`)).toBeVisible();

    // Shared fields should be visible as chips
    const sharedFieldsContainer = reviewerPage.getByTestId(`shared-fields-${id1}`);
    await expect(sharedFieldsContainer).toBeVisible();
    await expect(sharedFieldsContainer.getByText('Matched on:')).toBeVisible();
    await expect(reviewerPage.getByTestId(`shared-field-${id1}-ChildName`)).toHaveText('ChildName: Test Patient');
    await expect(reviewerPage.getByTestId(`shared-field-${id1}-Age`)).toHaveText('Age: 8');
  });

  reviewerTest('shows expandable field details', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const id1 = '44444444-4444-4444-4444-444444444444';
    const similar = {
      items: [
        createSimilarCaseDto({
          documentId: id1,
          summary: 'Subject: Test Patient. Category: ChildWelfare.',
          metadata: { ChildName: 'Test Patient', Age: '8', ReasonForReferral: 'Neglect' },
          sharedFields: {},
        }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, similar);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Expand similar cases panel
    await reviewerPage.getByText('Similar Cases').click();

    // Expand field details
    await reviewerPage.getByText('Field Details').click();
    await expect(reviewerPage.getByText('ChildName')).toBeVisible();
    await expect(reviewerPage.getByText('Neglect')).toBeVisible();
  });

  reviewerTest('does not show shared fields section when empty', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const id1 = '55555555-5555-5555-5555-555555555555';
    const similar = {
      items: [
        createSimilarCaseDto({
          documentId: id1,
          metadata: { ChildName: 'Someone Else' },
          sharedFields: {},
        }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, similar);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByText('Similar Cases').click();
    await expect(reviewerPage.getByTestId(`shared-fields-${id1}`)).not.toBeVisible();
  });

  reviewerTest('shows loading state', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    // Delay the similar-cases response
    await reviewerPage.route(`**/api/reviews/${doc.id}/similar-cases`, async route => {
      await new Promise(resolve => setTimeout(resolve, 2000));
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: { items: [] }, error: null }),
      });
    });
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Expand panel to see loading
    await reviewerPage.getByText('Similar Cases').click();
    await expect(reviewerPage.getByTestId('similar-loading')).toBeVisible();
  });
});
