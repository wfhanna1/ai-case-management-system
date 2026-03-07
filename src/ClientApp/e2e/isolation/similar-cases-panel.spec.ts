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

  reviewerTest('shows expandable field details', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const id1 = '33333333-3333-3333-3333-333333333333';
    const similar = {
      items: [
        createSimilarCaseDto({
          documentId: id1,
          summary: 'Subject: Test Patient. Category: ChildWelfare.',
          metadata: { ChildName: 'Test Patient', Age: '8', ReasonForReferral: 'Neglect' },
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

  reviewerTest('displays cases sorted by descending similarity score', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const id1 = 'aaaa1111-1111-1111-1111-111111111111';
    const id2 = 'bbbb2222-2222-2222-2222-222222222222';
    const id3 = 'cccc3333-3333-3333-3333-333333333333';
    const similar = {
      items: [
        createSimilarCaseDto({ documentId: id1, score: 0.95 }),
        createSimilarCaseDto({ documentId: id2, score: 0.82 }),
        createSimilarCaseDto({ documentId: id3, score: 0.61 }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, similar);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByText('Similar Cases').click();

    const cases = reviewerPage.locator('[data-testid^="similar-case-"]');
    await expect(cases).toHaveCount(3);

    // Verify DOM order matches score-descending order
    await expect(cases.nth(0)).toHaveAttribute('data-testid', `similar-case-${id1}`);
    await expect(cases.nth(1)).toHaveAttribute('data-testid', `similar-case-${id2}`);
    await expect(cases.nth(2)).toHaveAttribute('data-testid', `similar-case-${id3}`);
  });

  reviewerTest('shows correct badge colors for score thresholds', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const idHigh = 'dddd4444-4444-4444-4444-444444444444';
    const idMedium = 'eeee5555-5555-5555-5555-555555555555';
    const idLow = 'ffff6666-6666-6666-6666-666666666666';
    const similar = {
      items: [
        createSimilarCaseDto({ documentId: idHigh, score: 0.95 }),
        createSimilarCaseDto({ documentId: idMedium, score: 0.75 }),
        createSimilarCaseDto({ documentId: idLow, score: 0.45 }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetSimilarCases(reviewerPage, doc.id, similar);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByText('Similar Cases').click();

    // High score (>=0.9) gets success color (green)
    const badgeHigh = reviewerPage.getByTestId(`score-badge-${idHigh}`);
    await expect(badgeHigh).toHaveText('95% match');
    await expect(badgeHigh).toHaveClass(/MuiChip-colorSuccess/);

    // Medium score (0.7-0.9) gets warning color (yellow/orange)
    const badgeMedium = reviewerPage.getByTestId(`score-badge-${idMedium}`);
    await expect(badgeMedium).toHaveText('75% match');
    await expect(badgeMedium).toHaveClass(/MuiChip-colorWarning/);

    // Low score (<0.7) gets default color (gray)
    const badgeLow = reviewerPage.getByTestId(`score-badge-${idLow}`);
    await expect(badgeLow).toHaveText('45% match');
    await expect(badgeLow).toHaveClass(/MuiChip-colorDefault/);
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
