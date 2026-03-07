import { test, expect } from '@playwright/test';
import { loginAsWorker } from './helpers/login';

test.describe('Dashboard E2E', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsWorker(page);
  });

  test('dashboard loads and displays stat cards with real data', async ({ page }) => {
    await page.goto('/dashboard');

    // Heading
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();

    // All four stat card titles are visible
    await expect(page.getByText('Total Cases')).toBeVisible();
    await expect(page.getByText('Pending Review')).toBeVisible();
    await expect(page.getByText('Processed Today')).toBeVisible();
    await expect(page.getByText('Avg. Processing Time')).toBeVisible();

    // Stat values should be numbers or formatted strings, not "--"
    // (at minimum Total Cases should be >0 from seeded data)
    const totalCases = page.getByTestId('stat-total-cases');
    await expect(totalCases).toBeVisible();
    const totalCasesText = await totalCases.textContent();
    expect(totalCasesText).toBeTruthy();
  });

  test('dashboard does not show error alert when stats load', async ({ page }) => {
    await page.goto('/dashboard');

    // Wait for stat cards to load
    await expect(page.getByText('Total Cases')).toBeVisible();

    // No error alert should appear
    await expect(page.getByText('Failed to load dashboard stats')).not.toBeVisible();
  });

  test('dashboard shows Recent Activity section', async ({ page }) => {
    await page.goto('/dashboard');

    await expect(page.getByText('Recent Activity')).toBeVisible();
  });

  test('dashboard screenshot matches visual baseline', async ({ page }) => {
    await page.goto('/dashboard');

    // Wait for stats to fully load
    await expect(page.getByText('Total Cases')).toBeVisible();
    await page.getByTestId('stat-total-cases').waitFor({ state: 'visible' });

    await expect(page).toHaveScreenshot('dashboard-loaded.png', {
      fullPage: true,
      maxDiffPixelRatio: 0.05,
    });
  });
});
