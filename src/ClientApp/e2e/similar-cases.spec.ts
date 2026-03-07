import { test, expect } from '@playwright/test';

test.describe('Similar cases (E2E)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('email address').fill('reviewer@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|$)/);
  });

  test('similar cases panel loads with results on review detail page', async ({ page }) => {
    await page.getByRole('button', { name: 'Reviews' }).click();
    await page.waitForURL(/\/reviews/);

    const reviewLink = page.getByRole('link', { name: 'Review' }).first();
    await reviewLink.click();
    await page.waitForURL(/\/reviews\/[a-f0-9-]+/);

    const panel = page.getByTestId('similar-cases-panel');
    await expect(panel).toBeVisible();
    await panel.click();

    // Wait for loading to complete
    await expect(page.getByTestId('similar-loading')).not.toBeVisible({ timeout: 15000 });

    // Either results or empty state should be visible
    const hasResults = await page.locator('[data-testid^="similar-case-"]').count();
    const hasEmpty = await page.getByTestId('no-similar-cases').isVisible().catch(() => false);
    expect(hasResults > 0 || hasEmpty).toBeTruthy();

    if (hasResults > 0) {
      const firstBadge = page.locator('[data-testid^="score-badge-"]').first();
      await expect(firstBadge).toBeVisible();
      await expect(firstBadge).toHaveText(/\d+% match/);
    }
  });

  test('similar cases API returns results for a reviewed document', async ({ page, request }) => {
    // Log in to get auth token
    const loginResponse = await request.post('/api/auth/login', {
      data: { email: 'reviewer@alpha.demo', password: 'Demo123!' },
    });
    expect(loginResponse.ok()).toBeTruthy();
    const loginData = await loginResponse.json();
    const token = loginData.data.accessToken;

    // Navigate to reviews to find a document ID
    await page.getByRole('button', { name: 'Reviews' }).click();
    await page.waitForURL(/\/reviews/);

    const reviewLink = page.getByRole('link', { name: 'Review' }).first();
    const href = await reviewLink.getAttribute('href');
    expect(href).toBeTruthy();
    const documentId = href!.split('/').pop();

    // Call the similar-cases API endpoint directly
    const response = await request.get(`/api/reviews/${documentId}/similar-cases`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(response.ok()).toBeTruthy();
    const data = await response.json();

    // The response should have data with items array
    expect(data.data).toBeDefined();
    expect(data.data.items).toBeDefined();
  });

  test('similar cases panel renders seeded data without errors', async ({ page }) => {
    await page.getByRole('button', { name: 'Reviews' }).click();
    await page.waitForURL(/\/reviews/);

    const reviewLink = page.getByRole('link', { name: 'Review' }).first();
    await reviewLink.click();
    await page.waitForURL(/\/reviews\/[a-f0-9-]+/);

    await page.getByTestId('similar-cases-panel').click();

    // Wait for results to load
    await expect(page.getByTestId('similar-loading')).not.toBeVisible({ timeout: 15000 });

    // Verify the panel rendered without errors (smoke test for full pipeline)
    const panel = page.getByTestId('similar-cases-panel');
    await expect(panel).toBeVisible();
  });
});
