import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';

workerTest.describe('Navigation responsiveness', () => {
  workerTest('shows hamburger menu on small screens', async ({ workerPage: page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    await expect(page.getByTestId('mobile-menu-btn')).toBeVisible();
  });

  workerTest('hides inline nav buttons on small screens', async ({ workerPage: page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    // Inline nav buttons should be hidden on mobile
    const dashboardBtn = page.getByRole('button', { name: 'Dashboard' });
    await expect(dashboardBtn).not.toBeVisible();
  });

  workerTest('hamburger menu opens drawer with nav links', async ({ workerPage: page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    await page.getByTestId('mobile-menu-btn').click();
    await expect(page.getByTestId('mobile-drawer')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-dashboard')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-upload')).toBeVisible();
    await expect(page.getByTestId('mobile-nav-documents')).toBeVisible();
  });

  workerTest('mobile drawer nav item navigates and closes drawer', async ({ workerPage: page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');

    await page.getByTestId('mobile-menu-btn').click();
    await page.getByTestId('mobile-nav-upload').click();

    await expect(page).toHaveURL(/\/upload/);
    await expect(page.getByTestId('mobile-drawer')).not.toBeVisible();
  });

  workerTest('shows inline nav buttons on large screens', async ({ workerPage: page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/dashboard');

    const dashboardBtn = page.getByRole('button', { name: 'Dashboard' });
    await expect(dashboardBtn).toBeVisible();
    await expect(page.getByTestId('mobile-menu-btn')).not.toBeVisible();
  });
});
