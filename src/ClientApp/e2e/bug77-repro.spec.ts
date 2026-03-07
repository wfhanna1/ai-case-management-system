import { test, expect } from '@playwright/test';

test.describe('Bug #77: Review status not saved after finalize', () => {
  test('finalize navigates to queue and document is no longer pending', async ({ page }) => {
    // Login as reviewer
    await page.goto('/login');
    await page.getByLabel('Email').fill('reviewer@alpha.demo');
    await page.getByLabel('Password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL('**/dashboard');

    // Navigate to review queue
    await page.goto('/reviews');
    await expect(page.getByText('Review Queue')).toBeVisible();

    // Wait for the table to load
    const firstRow = page.locator('tbody tr').first();
    await expect(firstRow.getByRole('button', { name: 'Review' })).toBeVisible({ timeout: 10000 });

    // Click Review on first document
    await firstRow.getByRole('button', { name: 'Review' }).click();
    await expect(page.getByText('Document Info')).toBeVisible({ timeout: 5000 });

    // Capture the document ID from the URL
    const docId = page.url().split('/reviews/')[1];

    // Start review if pending
    const statusChip = page.getByTestId('review-status');
    const currentStatus = await statusChip.textContent();
    if (currentStatus === 'PendingReview') {
      await page.getByTestId('start-review-btn').click();
      await expect(statusChip).toContainText('InReview', { timeout: 5000 });
    }

    // Finalize
    await page.getByTestId('finalize-btn').click();
    await expect(page.getByText('Are you sure you want to finalize')).toBeVisible();
    await page.getByTestId('confirm-finalize-btn').click();

    // After finalization, should auto-navigate back to review queue
    await expect(page.getByText('Review Queue')).toBeVisible({ timeout: 10000 });
    await page.waitForURL('**/reviews');

    // Verify document is Finalized by navigating back to it
    await page.goto(`/reviews/${docId}`);
    await expect(page.getByText('Document Info')).toBeVisible({ timeout: 5000 });
    const finalStatus = await page.getByTestId('review-status').textContent();
    expect(finalStatus).toBe('Finalized');
  });
});
